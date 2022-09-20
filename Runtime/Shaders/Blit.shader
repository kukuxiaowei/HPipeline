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
            #pragma vertex vertFullScreen
            #pragma fragment frag

            #include "Common.cginc"

            sampler2D _Source;
            float4 _BlitScaleOffset;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_Source, i.uv * _BlitScaleOffset.xy + _BlitScaleOffset.zw);

                return col;
            }
            ENDCG
        }
    }
}
