Shader "Hidden/Blit"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Blit"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _Source;
            float4 _BlitScaleOffset;

            v2f vert (uint vertexID : SV_VertexID)
            {
                v2f o;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.vertex = float4(uv * 2.0 - 1.0, UNITY_NEAR_CLIP_VALUE, 1.0);
#if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
#endif
                o.uv = uv * _BlitScaleOffset.xy + _BlitScaleOffset.zw;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_Source, i.uv);

                return col;
            }
            ENDCG
        }
    }
}
