Shader "Hidden/IntegrateBRDF"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vertFullScreen
            #pragma fragment frag

            #include "Common.cginc"
            #include "BRDF.cginc"
            #define SAMPLE_COUNT 1024

            //http://holger.dammertz.org/stuff/notes_HammersleyOnHemisphere.html
            float RadicalInverse_VdC(uint bits) 
            {
                bits = (bits << 16u) | (bits >> 16u);
                bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
                bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
                bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
                bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
                return float(bits) * 2.3283064365386963e-10; // / 0x100000000
            }

            float2 Hammersley(uint i, uint N)
            {
                return float2(float(i)/float(N), RadicalInverse_VdC(i));
            }  

            //https://agraphicsguynotes.com/posts/sample_microfacet_brdf/
            float3 ImportanceSampleGGX(float2 Xi, float3 N, float roughness)
            {
                float a = roughness * roughness;

                float phi = 2.0 * PI * Xi.x;
                float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a * a - 1.0) * Xi.y));
                float sinTheta = sqrt(1.0 - cosTheta * cosTheta);

                // from spherical coordinates to cartesian coordinates
                float3 H;
                H.x = cos(phi) * sinTheta;
                H.y = sin(phi) * sinTheta;
                H.z = cosTheta;

                // from tangent-space vector to world-space sample vector
                float3 up        = abs(N.z) < 0.999 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
                float3 tangent   = normalize(cross(up, N));
                float3 bitangent = cross(N, tangent);

                float3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
                return normalize(sampleVec);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float NdotV = i.uv.x;
                float roughness = i.uv.y;
                float k = roughness * roughness * 0.5;

                float3 N = float3(0.0, 0.0, 1.0);
                float3 V = float3(sqrt(1 - NdotV * NdotV), 0, NdotV);

                float R = 0;
                float G = 0;
                for (uint i = 0; i < SAMPLE_COUNT; ++i)
                {
                    float2 Xi = Hammersley(i, SAMPLE_COUNT);
                    float3 H  = ImportanceSampleGGX(Xi, N, roughness);
                    float3 L  = normalize(2.0 * dot(V, H) * H - V);

                    float NdotH = max(dot(N, H), 0);
                    float NdotV = max(dot(N, V), 0);
                    float NdotL = max(dot(N, L), 0);
                    float HdotV = max(dot(H, V), 0);

                    if (NdotL > 0.0)
                    {
                        //pdf = D(H) * NdotH / (4 * HdotL)
                        //weight = D * V * NdotL / pdf
                        float Vis = V_Smith(NdotV, NdotL, k);
                        float weight = Vis * NdotL * HdotV * 4.0 / NdotH;

                        R += Pow5(1.0 - HdotV) * weight;
                        G += weight;
                    }
                }

                return float4(R, G, 0.0, 0.0) / SAMPLE_COUNT;
            }
            ENDCG
        }
    }
}
