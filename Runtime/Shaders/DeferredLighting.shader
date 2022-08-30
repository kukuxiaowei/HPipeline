Shader "Hidden/DeferredLighting"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "DeferredLighting"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                //float4 vertex : SV_POSITION;
            };

            v2f vert (uint vertexID : SV_VertexID, out float4 pos : SV_POSITION)
            {
                v2f o;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                pos = float4(uv * 2.0 - 1.0, UNITY_NEAR_CLIP_VALUE, 1.0);
#if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
#endif
                o.uv = uv;
                return o;
            }

            struct LightData
            {
                float4 position;
                float4 color;
            };

            sampler2D _GBuffer0;
            sampler2D _GBuffer1;
            sampler2D _GBuffer2;
            sampler2D _DepthBuffer;
            sampler3D _LightsCullTexture;
            StructuredBuffer<uint> _LightIndexBuffer;
            StructuredBuffer<LightData> _LightData;
            float4 _MainLightPosition;
            float4 _MainLightColor;
            float4x4 _ScreenToWorldMatrix;

            #define ClusterRes 32
            #define ClustersNumZ 16
            float4 _ClustersNumData;//ClustersNumX, ClustersNumY

            float3 Lambert(float3 diffuse)
            {
                return diffuse * UNITY_INV_PI;
            }

            //Trowbridge-Reitz GGX
            float D_GGX(float a2, float NdotH)
            {
                float denominator = NdotH * NdotH * (a2 - 1.0) + 1;
                return a2 / (UNITY_PI * denominator * denominator);
            }

            //Smith’s Schlick-GGX
            float V_Smith(float NdotV, float NdotL, float k)
            {
                float oneMinusK = 1.0 - k;
                float rcpLeft = NdotV * oneMinusK + k;
                float rcpRight = NdotL * oneMinusK + k;
                return 0.25 / (rcpLeft * rcpRight);
            }

            //Fresnel-Schlick Approximation
            float3 F_Schlick(float3 F0, float HdotV)
            {
                float f = 1.0 - HdotV;
                f = f * f * f * f * f;
                return F0 + (1.0 - F0) * f;
            }

            float3 BRDF(float3 diffuse, float3 F0, float roughness, float3 N, float3 L, float3 V)
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
                return (Lambert(diffuse) + specular) * NdotL;
            }

            float4 frag (v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                float3 normalWS = tex2D(_GBuffer0, i.uv).xyz * 2.0 - 1.0;
                float4 gBufferData1 = tex2D(_GBuffer1, i.uv);
                float3 diffuse = gBufferData1.rgb;
                float3 emission = diffuse * gBufferData1.a;
                float4 gBufferData2 = tex2D(_GBuffer2, i.uv);
                float3 specular = gBufferData2.rgb;
                float smoothness = gBufferData2.a;
                float roughness = 1.0 - smoothness;
                
                float depth = tex2D(_DepthBuffer, i.uv).r;
                float4 posCS = float4(i.uv * 2.0 - 1.0, depth, 1.0);
                float4 posWS = mul(_ScreenToWorldMatrix, posCS);
                posWS /= posWS.w;
                float3 view = normalize(_WorldSpaceCameraPos - posWS.xyz);
                
                float3 brdf = BRDF(diffuse, specular, roughness, normalWS, _MainLightPosition, view);
                float3 col = brdf * _MainLightColor.rgb + emission;

                float z = Linear01Depth(depth);
                float2 xy = screenPos.xy / (ClusterRes * _ClustersNumData.xy);
                uint2 lightStartIdxAndCount = tex3D(_LightsCullTexture, float3(xy, z));
                for (uint i = 0; i < lightStartIdxAndCount.y; ++i)
                {
                    uint lightIdx = _LightIndexBuffer[lightStartIdxAndCount.x + i];
                    LightData light = _LightData[lightIdx];

                    float3 lightDir = posWS.xyz - light.position.xyz;
                    float lightDis = length(lightDir);
                    lightDir = lightDir / lightDis;
                    float lightRangeRcp = light.position.w;
                    float atten = saturate(1.0 - lightDis * lightDis * lightRangeRcp * lightRangeRcp);
                    brdf = BRDF(diffuse, specular, roughness, normalWS, lightDir, view);
                    col += brdf * light.color.rgb * atten;
                }
                
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
}
