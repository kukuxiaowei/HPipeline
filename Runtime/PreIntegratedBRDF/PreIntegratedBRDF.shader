Shader "Hidden/PreIntegratedBRDF"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
			HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
			#include "PreIntegratedBRDF.cs.hlsl"

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert(uint vertexID : SV_VertexID)
			{
				v2f o;

				o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
				o.uv = GetFullScreenTriangleTexCoord(vertexID);

				return o;
			}

			float4 frag (v2f i) : SV_Target
            {
				float2 preIntegratedTexCoord = RemapHalfTexelCoordTo01(i.uv, PREINTEGRATEDTEXTURE_RESOLUTION);
				float NdotV = preIntegratedTexCoord.x;
				float perceptualRoughness = preIntegratedTexCoord.y;

				float4 preIntegrateBRDF = IntegrateGGXAndDisneyDiffuseFGD(NdotV, PerceptualRoughnessToRoughness(perceptualRoughness));

				return float4(preIntegrateBRDF.xyz, 1.0);
            }
			ENDHLSL
        }
    }
}
