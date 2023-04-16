Shader "Hidden/Blit"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Blit"

			HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            sampler2D _BlitTexture;
            float4 _BlitScaleBias;

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert(uint vertexID : SV_VertexID)
			{
				v2f o;

				float4 pos = GetFullScreenTriangleVertexPosition(vertexID);
				float2 uv = GetFullScreenTriangleTexCoord(vertexID);

				o.vertex = pos;
				o.uv = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
				return o;
			}

			float4 frag (v2f i) : SV_Target
            {
                return tex2D(_BlitTexture, i.uv);
            }
            ENDHLSL
        }
    }
}
