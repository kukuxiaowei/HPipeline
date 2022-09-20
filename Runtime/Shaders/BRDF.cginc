#ifndef __BRDF_INCLUDE__
#define __BRDF_INCLUDE__

#define PI 3.14159265359f

float Pow5(float x)
{
    return x * x * x * x * x;
}

//Trowbridge-Reitz GGX
float D_GGX(float a2, float NdotH)
{
    float denominator = NdotH * NdotH * (a2 - 1.0) + 1;
    return a2 / (PI * denominator * denominator);
}

//Smith’s Schlick-GGX
float V_Smith(float NdotV, float NdotL, float k)
{
    float oneMinusK = 1.0 - k;
    float rcpLeft = NdotV * oneMinusK + k;
    float rcpRight = NdotL * oneMinusK + k;
    return 0.25 / (rcpLeft * rcpRight);
}

//Fresnel-Schlick Approximation
float3 F_Schlick(float3 F0, float HdotV)
{
    return F0 + (1.0 - F0) * Pow5(1.0 - HdotV);
}

#endif