Shader "Hidden/DeferredLighting"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #pragma target 5.0
        ENDCG

        Pass
        {
            Name "DeferredLighting"
            CGPROGRAM
            #pragma vertex vertFullScreen
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Common.cginc"
            #include "BRDF.cginc"

            struct LightData
            {
                float4 position;
                float4 spotDirection;
                float4 color;
            };

            Texture2D<float4> _GBuffer0;
            Texture2D<float4> _GBuffer1;
            Texture2D<float4> _GBuffer2;
            Texture2D<float3> _BakedGI;
            Texture2D<float> _DepthBuffer;
            Texture3D<uint2> _LightsCullTexture;
            StructuredBuffer<uint> _LightIndexBuffer;
            StructuredBuffer<LightData> _LightData;
            float4 _MainLightPosition;
            float4 _MainLightColor;
            float4x4 _ScreenToWorldMatrix;

            sampler2D _IntegratedBRDFTexture;
            int _ProbesCount;
            UNITY_DECLARE_TEXCUBEARRAY(_ProbesTexture);

            #define ClusterRes 32
            #define ClustersNumZ 16
            float4 _ClustersNumData;//ClustersNumX, ClustersNumY
            float4 _ClusterSizeData;//ClusterSizeX, ClusterSizeY, ClusterSizeZ, ClusterSizeZRcp

            float Pow4(float x)
            {
                x = x * x;
                x = x * x;
                return x;
            }

            float3 Lambert(float3 diffuse)
            {
                return diffuse * UNITY_INV_PI;
            }

            float3 BRDF(float3 diffuse, float3 F0, float roughness, float3 N, float3 L, float3 V, float multiScatterEnergy)
            {
                float a = roughness * roughness;
                float a2 = a * a;
                float k = roughness + 1.0;
                k = k * k * 0.125;
                float3 H = normalize(V + L);
                float NdotH = max(dot(N, H), 0);
                float NdotV = max(dot(N, V), 0);
                float NdotL = max(dot(N, L), 0);
                float HdotV = max(dot(H, V), 0);

                float3 specular = D_GGX(a2, NdotH) * V_Smith(NdotV, NdotL, k) * F_Schlick(F0, HdotV);
                specular *= multiScatterEnergy;
                return (Lambert(diffuse) + specular) * NdotL;
            }

            float DistanceAttenuation(float distanceSqr, float lightRange)
            {
                //atten = smooth/x^2, smooth = saturate(1-(x/r)^4)^2
                float lightDisRcp = rsqrt(distanceSqr);
                float attenSmooth = saturate(1.0 - Pow4(rcp(lightDisRcp * lightRange)));
                float atten = attenSmooth * lightDisRcp;
                return atten * atten;
            }

            float4 frag (v2f input) : SV_Target
            {
                uint2 posSS = input.vertex.xy;
                float3 normalWS = _GBuffer0[posSS] * 2.0 - 1.0;
                float4 gBufferData1 = _GBuffer1[posSS];
                float3 diffuse = gBufferData1.rgb;
                float3 emission = diffuse * gBufferData1.a;
                float3 indirectDiffuse = _BakedGI[posSS] * diffuse;
                float4 gBufferData2 = _GBuffer2[posSS];
                float3 specular = gBufferData2.rgb;
                float smoothness = gBufferData2.a;
                float roughness = 1.0 - smoothness;
                
                float depth = _DepthBuffer[posSS];
                
                #if UNITY_REVERSED_Z
				float3 posCS = float3(input.uv, 1.0 - depth) * 2.0 - 1.0;
                #else
                float3 posCS = float3(input.uv, depth) * 2.0 - 1.0;
				#endif
                
                float4 posWS = mul(_ScreenToWorldMatrix, float4(posCS, 1.0));
                posWS /= posWS.w;
                float3 view = normalize(_WorldSpaceCameraPos - posWS.xyz);
                float3 r = reflect(-view, normalWS);
                
                float3 col = indirectDiffuse;

                float NdotV = max(dot(normalWS, view), 0);
                float reflectivity = tex2Dlod(_IntegratedBRDFTexture, float4(NdotV, roughness, 0.0, 0.0)).y;
                float multiScatterEnergy = 1.0 + specular * (1.0 / reflectivity - 1.0);

                //DirectionalLighting
                float3 brdf = BRDF(diffuse, specular, roughness, normalWS, _MainLightPosition, view, multiScatterEnergy);
                col += brdf * _MainLightColor.rgb + emission;

                //PunctualLighting
                uint z = (uint)(LinearEyeDepth(depth) * _ClusterSizeData.w);
                z = clamp(z, 0, 15);
                uint2 xy = posSS / ClusterRes;
                uint2 lightStartIdxAndCount = _LightsCullTexture[uint3(xy, z)];
                for (uint i = 0; i < lightStartIdxAndCount.y; ++i)
                {
                    uint lightIdx = _LightIndexBuffer[lightStartIdxAndCount.x + i];
                    LightData light = _LightData[lightIdx];

                    float3 lightDir = light.position.xyz - posWS.xyz;
                    float lightDisSqr = dot(lightDir, lightDir);
                    lightDir = lightDir * rsqrt(lightDisSqr);

                    float atten = DistanceAttenuation(lightDisSqr, light.position.w);

                    //SpotLight
                    float cosAngle = light.spotDirection.w;
                    if(cosAngle > 0.0)
                    {
                        float cosInnerAngle = light.color.w;
                        float angleAtten = saturate((dot(-light.spotDirection.xyz, lightDir) - cosAngle) / max(cosInnerAngle - cosAngle, 0.001));
                        angleAtten = angleAtten * angleAtten;
                        atten *= angleAtten;
                    }
                    
                    brdf = BRDF(diffuse, specular, roughness, normalWS, lightDir, view, multiScatterEnergy);
                    col += brdf * light.color.rgb * atten;
                }
                
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
}
