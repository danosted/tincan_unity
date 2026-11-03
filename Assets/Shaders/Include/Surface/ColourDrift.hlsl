// Surface style "colour drift" (Features/Atmosphere/ColourDriftSurfaceStyle.cs; keyword _TINCAN_SURFACE_DRIFT): large,
// faint patches a little warmer or cooler than the base colour, like paint laid on unevenly. It changes the albedo
// only; the bands are untouched. _TinCanSurfaceParams: x strength 0..1, y patch size (m).
#ifndef TINCAN_SURFACE_DRIFT_INCLUDED
#define TINCAN_SURFACE_DRIFT_INCLUDED
#define TINCAN_STYLE_ALBEDO

#include "Noise.hlsl"

half3 TinCanStyleAlbedo(half3 albedo, float3 styleCoord)
{
    float3 p = styleCoord / max(_TinCanSurfaceParams.y, 0.01);
    float n = TinCanValueNoise3(p) * 0.65 + TinCanValueNoise3(p * 2.3 + 17.0) * 0.35;
    // -strength (cool) .. +strength (warm)
    half drift = (half)((n * 2.0 - 1.0) * _TinCanSurfaceParams.x);
    half3 warm = half3(1.07h, 1.0h, 0.88h);
    half3 cool = half3(0.92h, 0.96h, 1.07h);
    half3 tint = drift > 0.0h ? lerp(half3(1, 1, 1), warm, drift) : lerp(half3(1, 1, 1), cool, -drift);
    return albedo * tint * (1.0h + drift * 0.12h);
}

#endif
