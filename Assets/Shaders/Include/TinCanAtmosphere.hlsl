// The project's air (.docs/ART_DIRECTION.md, "Light and shading"; plan .docs/plans/look-slice.md, L2): height fog in
// bands, densest at the cloud sea and thinning upward, plus distance haze, coloured by the angle to the sun (warm toward
// it, cool away). The Atmosphere feature's AtmospherePresenter sets the globals each frame; without it (the test range)
// opaque surfaces take Unity's plain fog and transparent ones are left alone. Shared by every TinCan shader, so
// everything fades the same way. Include after Core.hlsl.
#ifndef TINCAN_ATMOSPHERE_INCLUDED
#define TINCAN_ATMOSPHERE_INCLUDED

float _TinCanAtmosphereOn;
float4 _TinCanSunwardFog;
float4 _TinCanAwayFog;
float4 _TinCanFogParams; // x density at the base height (per m), y falloff (per m), z base height, w haze (per m)
float4 _TinCanFogShape;  // x the most the fog may cover, y how tightly the sunward colour gathers around the sun

// How much of what lies at positionWS the air covers, 0..max, and the air's colour along that view.
void TinCanAir(float3 positionWS, out half amount, out half3 colour)
{
    float3 ray = positionWS - GetCameraPositionWS();
    float distance = length(ray);
    float3 direction = ray / max(distance, 1e-4);

    // Exponential height fog, integrated along the ray: density(h) = d0 * exp(-falloff * (h - base)).
    float falloff = max(_TinCanFogParams.y, 1e-4);
    float above = max(GetCameraPositionWS().y - _TinCanFogParams.z, 0.0);
    float rise = falloff * ray.y;
    float alongRay = abs(rise) > 1e-3 ? (1.0 - exp(-rise)) / rise : 1.0 - 0.5 * rise;
    float heightFog = _TinCanFogParams.x * exp(-falloff * above) * distance * max(alongRay, 0.0);
    float haze = _TinCanFogParams.w * distance;
    amount = (half)min(1.0 - exp(-(heightFog + haze)), _TinCanFogShape.x);

    half sunward = (half)pow(saturate(dot(direction, _MainLightPosition.xyz) * 0.5 + 0.5), _TinCanFogShape.y);
    colour = lerp(_TinCanAwayFog.rgb, _TinCanSunwardFog.rgb, sunward);
}

// An opaque surface's colour seen through the air; unityFogFactor (ComputeFogFactor) is the plain fog's fallback.
half3 TinCanAtmosphere(half3 colour, float3 positionWS, half unityFogFactor)
{
    if (_TinCanAtmosphereOn < 0.5)
    {
        return MixFog(colour, unityFogFactor);
    }

    half amount;
    half3 air;
    TinCanAir(positionWS, amount, air);
    return lerp(colour, air, amount);
}

// A premultiplied-alpha surface (clouds) seen through the air; unchanged without the atmosphere.
half4 TinCanAtmospherePremultiplied(half4 colour, float3 positionWS)
{
    if (_TinCanAtmosphereOn < 0.5)
    {
        return colour;
    }

    half amount;
    half3 air;
    TinCanAir(positionWS, amount, air);
    colour.rgb = lerp(colour.rgb, air * colour.a, amount);
    return colour;
}

#endif
