//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef LIGHTDATA_CS_HLSL
#define LIGHTDATA_CS_HLSL
//
// HPipeline.LightCategory:  static fields
//
#define LIGHTCATEGORY_PUNCTUAL (0)
#define LIGHTCATEGORY_ENV (1)
#define LIGHTCATEGORY_COUNT (2)

// Generated from HPipeline.DirectionalLightData
// PackingRules = Exact
struct DirectionalLightData
{
    float4 positionWS;
    float4 color;
};

// Generated from HPipeline.PunctualLightData
// PackingRules = Exact
struct PunctualLightData
{
    float3 positionWS;
    float range;
    float3 color;
    float spotAngleScale;
    float3 forward;
    float spotAngleOffset;
};

// Generated from HPipeline.EnvLightData
// PackingRules = Exact
struct EnvLightData
{
    int envIndex;
    float3 positionWS;
    float range;
    float blendDistance;
    float2 padding;
};

// Generated from HPipeline.LightBound
// PackingRules = Exact
struct LightBound
{
    float3 position;
    float range;
    float3 forward;
    float spotCosAngle;
    float spotSinAngle;
    int category;
    uint categoryOffset;
    uint padding;
};


#endif
