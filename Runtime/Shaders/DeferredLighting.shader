Shader "Hidden/DeferredLighting"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "DeferredLighting"

			HLSLPROGRAM
			#pragma target 4.5
			#pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

            #pragma vertex vert
            #pragma fragment frag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
			#include "Input.hlsl"
			#include "Common.hlsl"
			#include "Packages/hpipeline/Runtime/PreIntegratedBRDF/PreIntegratedBRDF.cs.hlsl"

            Texture2D<float4> _GBuffer0;
            Texture2D<float4> _GBuffer1;
            Texture2D<float4> _GBuffer2;
            Texture2D<float3> _BakedGI;
            Texture2D<float> _DepthBuffer;
			StructuredBuffer<uint> _ClusterPackingOffset;
			StructuredBuffer<uint> _ClusterLights;
			StructuredBuffer<DirectionalLightData> _DirectionalLightDatas;
			int _DirectionalLightCount;
            StructuredBuffer<PunctualLightData> _PunctualLightDatas;
            StructuredBuffer<EnvLightData> _EnvLightDatas;
			float4x4 _ScreenToWorld;

            sampler2D _PreIntegratedBRDF;
			TEXTURECUBE_ARRAY(_EnvCubemapArray);
			SAMPLER(s_trilinear_clamp_sampler);

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 positionCS : SV_POSITION;
			};

			v2f vert(uint vertexID : SV_VertexID)
			{
				v2f o;

				o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
				o.uv = GetFullScreenTriangleTexCoord(vertexID);

				return o;
			}

            float3 BRDF(float3 diffuse, float3 F0, float roughness, float3 N, float3 L, float3 V, float3 multiScatterEnergy)
            {
                float3 H = normalize(V + L);
                float NdotH = max(dot(N, H), 0.0);
                float NdotV = max(dot(N, V), 0.0);
                float NdotL = max(dot(N, L), 0.0);
                float HdotV = max(dot(H, V), 0.0);

                float3 specular = D_GGX(roughness, NdotH) * V_SmithJointGGX(NdotL, NdotV, roughness) * F_Schlick(F0, HdotV);
                specular *= multiScatterEnergy;
                return (diffuse * Lambert() + specular) * NdotL;
            }

            float4 frag (v2f input) : SV_Target
            {
                uint2 positionSS = input.positionCS.xy;

				float4 gBufferData0 = _GBuffer0[positionSS];
				float4 gBufferData1 = _GBuffer1[positionSS];
				float4 gBufferData2 = _GBuffer2[positionSS];
				float3 bakedGI      = _BakedGI[positionSS];
				float  depth        = _DepthBuffer[positionSS];

                float3 normalWS        = gBufferData0.xyz * 2.0 - 1.0;
                float3 diffuse         = gBufferData1.rgb;
                float3 emission        = diffuse * gBufferData1.a;
                float3 indirectDiffuse = bakedGI * diffuse;
                float3 F0              = gBufferData2.rgb;
                float  smoothness      = gBufferData2.a;

                float perceptualRoughness = 1.0 - smoothness;
				float roughness = perceptualRoughness * perceptualRoughness;
                
				float4 positionWS = mul(_ScreenToWorld, float4(positionSS, depth, 1.0));
				positionWS = positionWS / positionWS.w;
                float3 view = normalize(_WorldSpaceCameraPos - positionWS.xyz);
                float3 R = reflect(-view, normalWS);
                
                float3 col = emission + indirectDiffuse;

				// PreIntegratedBRDF
                float NdotV = max(dot(normalWS, view), 0);
				float2 preIntegratedTexCoord = Remap01ToHalfTexelCoord(float2(NdotV, perceptualRoughness), PREINTEGRATEDTEXTURE_RESOLUTION);
                float3 integratedBRDF = tex2Dlod(_PreIntegratedBRDF, float4(preIntegratedTexCoord, 0.0, 0.0)).xyz;
                float3 multiScatterEnergy = (1.0).xxx + F0 * (1.0 / integratedBRDF.y - 1.0);

                // DirectionalLighting
				for (int i = 0; i < _DirectionalLightCount; ++i)
				{
					DirectionalLightData light = _DirectionalLightDatas[i];

					float3 brdf = BRDF(diffuse, F0, roughness, normalWS, light.positionWS.xyz, view, multiScatterEnergy);
					col += brdf * light.color.rgb;
				}

				// LightGrid
				float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
				uint slice = (uint) clamp(ClusterLinearDepthToSlice(linearDepth), 0, CLUSTER_DEPTH - 1);
				uint2 tileID = positionSS / CLUSTER_TILE_SIZE;

                // PunctualLighting
				uint punctualLightStartIndex, punctualLightCount;
				uint clusterIndex = ClusterIndex(tileID.x, tileID.y, slice, LIGHTCATEGORY_PUNCTUAL);
				UnpackClusterOffset(_ClusterPackingOffset[clusterIndex], punctualLightStartIndex, punctualLightCount);

                for (uint j = 0; j < punctualLightCount; ++j)
                {
                    uint lightIdx = _ClusterLights[punctualLightStartIndex + j];
					PunctualLightData light = _PunctualLightDatas[lightIdx];

                    float3 lightDir = light.positionWS - positionWS.xyz;
                    float lightDistSq = dot(lightDir, lightDir);
					float lightDistRcp = rsqrt(lightDistSq);
                    lightDir = lightDir * lightDistRcp;

					float attenuation = SmoothWindowedDistanceAttenuation(lightDistSq, lightDistRcp, 1.0 / (light.range * light.range), 1.0f)
						* SmoothAngleAttenuation(dot(-lightDir, light.forward), light.spotAngleScale, light.spotAngleOffset);
                    
					float3 brdf = BRDF(diffuse, F0, roughness, normalWS, lightDir, view, multiScatterEnergy);
                    col += brdf * light.color * attenuation;
                }

				// EnvLighting
				uint envLightStartIndex, envLightCount;
				clusterIndex = ClusterIndex(tileID.x, tileID.y, slice, LIGHTCATEGORY_ENV);
				UnpackClusterOffset(_ClusterPackingOffset[clusterIndex], envLightStartIndex, envLightCount);

				float totalWeight = 0.0;
				float mipmapLevel = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
				for (uint k = 0; k < envLightCount; ++k)
				{
					if (totalWeight < 0.99)
					{
						uint envlightIdx = _ClusterLights[envLightStartIndex + k];
						EnvLightData envlight = _EnvLightDatas[envlightIdx];

						float distance = length(positionWS.xyz - envlight.positionWS);
						float weight = saturate(1.0 - max(distance - envlight.range + envlight.blendDistance, 0.0) / max(envlight.blendDistance, 0.0001));
						weight = Smoothstep01(weight);
						float accumulatedWeight = totalWeight + weight;
						totalWeight = saturate(accumulatedWeight);
						weight -= saturate(accumulatedWeight - totalWeight);

						float3 prefilteredColor = SAMPLE_TEXTURECUBE_ARRAY_LOD(_EnvCubemapArray, s_trilinear_clamp_sampler, R, envlight.envIndex, mipmapLevel).rgb;
						float3 envLighting = prefilteredColor * (((1.0).xxx - F0) * integratedBRDF.x + F0 * integratedBRDF.y);
						col += envLighting * weight;
					}
				}

				if (totalWeight < 0.99)
				{
					float4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(_GlossyEnvironmentCubeMap, sampler_GlossyEnvironmentCubeMap, R, mipmapLevel);

#if defined(UNITY_USE_NATIVE_HDR) || defined(UNITY_DOTS_INSTANCING_ENABLED)
					col += (1.0f - totalWeight) * encodedIrradiance.rbg;
#else
					col += (1.0f - totalWeight) * DecodeHDREnvironment(encodedIrradiance, _GlossyEnvironmentCubeMap_HDR);
#endif
				}

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
