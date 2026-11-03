// Surface style "brush edge" (Features/Atmosphere/BrushEdgeSurfaceStyle.cs; keyword _TINCAN_SURFACE_BRUSH): the step
// between the lit and shadow bands breaks into ragged dabs, like a brush stroke's edge. It nudges N.L up and down around
// its true value before the bands, so the step roughens but stays where it was; far from the step nothing changes.
// _TinCanSurfaceParams: x strength (how far the edge wanders, in N.L), y stroke size (m).
#ifndef TINCAN_SURFACE_BRUSH_INCLUDED
#define TINCAN_SURFACE_BRUSH_INCLUDED
#define TINCAN_STYLE_NDOTL

#include "Noise.hlsl"

half TinCanStyleNDotL(half nDotL, float3 styleCoord)
{
    // Stretched along one axis, so the edge breaks into strokes rather than round blobs.
    float3 p = styleCoord / max(_TinCanSurfaceParams.y, 0.01) * float3(1.0, 3.0, 1.0);
    float n = TinCanValueNoise3(p) * 0.6 + TinCanValueNoise3(p * 2.7 + 31.0) * 0.4;
    return nDotL + (half)((n - 0.5) * _TinCanSurfaceParams.x);
}

#endif
