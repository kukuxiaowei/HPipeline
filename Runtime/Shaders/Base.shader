Shader "HPipleline/Base"
{
    Properties
    {
        _Albedo ("Albedo", 2D) = "white" {}
        [NoScaleOffset]_Normal ("Normal", 2D) = "bump" {}
        [NoScaleOffset]_Smooth_Map ("Smooth Map", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        [NoScaleOffset]_Metallic_Map ("Metallic Map", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Tags { "LightMode"="GBuffer" }
            
            Name "Base"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 tbn0 : TEXCOORD1;
                float4 tbn1 : TEXCOORD2;
                float4 tbn2 : TEXCOORD3;
            };

            sampler2D _Albedo;
            float4 _Albedo_ST;
            sampler2D _Normal;
            sampler2D _Smooth_Map;
            half _Smoothness;
            sampler2D _Metallic_Map;
            half _Metallic;

            v2f vert (appdata v)
            {
                v2f o;
                float3 posWS = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1));
                float3 normalWS = UnityObjectToWorldNormal(v.normal);
                float3 tangentWS = UnityObjectToWorldDir(v.tangent);
                float tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                float3 binormalWS = normalize(cross(normalWS, tangentWS) * tangentSign);
                o.tbn0 = float4(tangentWS.x, binormalWS.x, normalWS.x, posWS.x);
                o.tbn1 = float4(tangentWS.y, binormalWS.y, normalWS.y, posWS.y);
                o.tbn2 = float4(tangentWS.z, binormalWS.z, normalWS.z, posWS.z);
                o.vertex = UnityWorldToClipPos(posWS);
                o.uv = TRANSFORM_TEX(v.uv, _Albedo);
                return o;
            }

            void frag (v2f i,
                out float4 gBuffer0 : SV_Target0,
                out half4 gBuffer1 : SV_Target1,
                out half4 gBuffer2 : SV_Target2)
            {
                half3 albedo = tex2D(_Albedo, i.uv);
                float4 packedNormal = tex2D(_Normal, i.uv);
                half smoothness = tex2D(_Smooth_Map, i.uv).x * _Smoothness;
                half metallic = tex2D(_Metallic_Map, i.uv).x * _Metallic;

                float3 tangentWS = float3(i.tbn0.x, i.tbn1.x, i.tbn2.x);
                float3 binormalWS = float3(i.tbn0.y, i.tbn1.y, i.tbn2.y);
                float3 normalWS = float3(i.tbn0.z, i.tbn1.z, i.tbn2.z);
                float3 normalTS = packedNormal * 2.0 - 1.0;
                normalWS = mul(normalTS, float3x3(tangentWS, binormalWS, normalWS));
                normalWS = normalize(normalWS);
                gBuffer0 = float4(normalWS * 0.5 + 0.5, 0);
                
                half3 diffuse = albedo * lerp(0.96, 0, metallic);
                half3 specular = lerp(0.04, albedo, metallic);
                gBuffer1 = half4(diffuse, 0);
                gBuffer2 = half4(specular, smoothness);
            }
            ENDCG
        }
    }
}
