// Smooth 3D value noise for the surface styles (Include/TinCanLighting.hlsl): cheap, no texture, and only ever used to
// vary colour or a band edge, so small float differences between platforms do not matter.
#ifndef TINCAN_SURFACE_NOISE_INCLUDED
#define TINCAN_SURFACE_NOISE_INCLUDED

float TinCanHash3(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// 0..1, smooth, one feature per unit.
float TinCanValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float x00 = lerp(TinCanHash3(i), TinCanHash3(i + float3(1, 0, 0)), u.x);
    float x10 = lerp(TinCanHash3(i + float3(0, 1, 0)), TinCanHash3(i + float3(1, 1, 0)), u.x);
    float x01 = lerp(TinCanHash3(i + float3(0, 0, 1)), TinCanHash3(i + float3(1, 0, 1)), u.x);
    float x11 = lerp(TinCanHash3(i + float3(0, 1, 1)), TinCanHash3(i + float3(1, 1, 1)), u.x);
    return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
}

#endif
