#ifndef __COMMON_INCLUDE__
#define __COMMON_INCLUDE__

struct v2f
{
    float2 uv : TEXCOORD0;
    float4 vertex : SV_POSITION;
};

v2f vertFullScreen (uint vertexID : SV_VertexID)
{
    v2f o;
    float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
    o.vertex = float4(uv * 2.0 - 1.0, UNITY_NEAR_CLIP_VALUE, 1.0);
    #if UNITY_UV_STARTS_AT_TOP
    uv.y = 1.0 - uv.y;
    #endif
    o.uv = uv;
    return o;
}

#endif