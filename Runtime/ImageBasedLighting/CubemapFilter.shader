Shader "Hidden/CubemapFilter"
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
		
			TEXTURECUBE(_CubeMap);
			SAMPLER(s_trilinear_clamp_sampler);

			int _FaceIndex;
			float _MipLevel;

			struct v2f
			{
				float2 nvc : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert(uint vertexID : SV_VertexID)
			{
				v2f o;

				float4 pos = GetFullScreenTriangleVertexPosition(vertexID);
				float2 uv = GetFullScreenTriangleTexCoord(vertexID);

				o.vertex = pos;
				o.nvc = uv * 2.0 - 1.0;
				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				float3 View = CubemapTexelToDirection(i.nvc, _FaceIndex);
				float3 Normal = View;
				float3x3 localToWorld = GetLocalFrame(Normal);

				float perceptualRoughness = MipmapLevelToPerceptualRoughness(_MipLevel);
				float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);

				const uint SAMPLE_COUNT = 1024u;
				float totalWeight = 0.0;
				float3 preFilteredColor = float3(0.0, 0.0, 0.0);
				for (uint i = 0; i < SAMPLE_COUNT; ++i)
				{
					float2 Xi = Hammersley2d(i, SAMPLE_COUNT);
					float3 L;
					float NdotL, NdotH, LdotH;
					SampleGGXDir(Xi, View, localToWorld, roughness, L, NdotL, NdotH, LdotH, true);

					if (NdotL > 0.0)
					{
						preFilteredColor += SAMPLE_TEXTURECUBE_LOD(_CubeMap, s_trilinear_clamp_sampler, L, 0).rgb * NdotL;
						totalWeight += NdotL;
					}
				}

				return float4(preFilteredColor / totalWeight, 1.0);
			}
			ENDHLSL
		}
    }
}
