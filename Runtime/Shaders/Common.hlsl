#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

#include "Packages/hpipeline/Runtime/RenderPipeline/BuildLightGrid.cs.hlsl"
#include "Packages/hpipeline/Runtime/RenderPipeline/LightData.cs.hlsl"

int _ClusterNumTileX;
int _ClusterNumTileY;
float4 _ClusterZParams;

uint ClusterIndex(uint x, uint y, uint z, uint category)
{
    return ((category * CLUSTER_DEPTH + z) * _ClusterNumTileY + y) * _ClusterNumTileX + x;
}

float ClusterSliceToLinearDepth(uint slice)
{
    float zScale = _ClusterZParams.x;
    float commonRatio = _ClusterZParams.y;
    float nearPlane = _ClusterZParams.z;
    // Sum of Geometric Progression
    float sum = (pow(abs(commonRatio), slice) - 1.0) / ((commonRatio - 1.0f) * zScale);
    return sum + nearPlane;
}

uint ClusterLinearDepthToSlice(float depth)
{
    float zScale = _ClusterZParams.x;
    float commonRatio = _ClusterZParams.y;
    float nearPlane = _ClusterZParams.z;
    // Inverse of Geometric Progression
    float sum = (depth - nearPlane) * zScale;
    return (uint) (log2(sum * (commonRatio - 1.0) + 1.0) / log2(commonRatio));
}

uint PackClusterOffset(uint offset, uint count)
{
    return (offset & CLUSTER_PACKING_OFFSET_MASK) | (min(count, CLUSTER_MAX_LIGHT) << CLUSTER_PACKING_OFFSET_BITS);
}

void UnpackClusterOffset(uint packedValue, out uint offset, out uint count)
{
    offset = packedValue & CLUSTER_PACKING_OFFSET_MASK;
    count = packedValue >> CLUSTER_PACKING_OFFSET_BITS;
}
#endif