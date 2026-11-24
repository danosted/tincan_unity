// The storm's region (plan .docs/plans/storm-crossing.md, ST2): where in the world the storm stands, so every peer draws
// it in the same place and it reads from afar. Set by the Storm feature (StormShaderGlobals). The shape across the disc
// (TinCanStormProfile) is kept in step with StormProcessor.Profile in C#, which weighs the light and air at the camera.
#ifndef TINCAN_STORM_INCLUDED
#define TINCAN_STORM_INCLUDED

// xy the centre on the map (world x, z), z the radius (0: no storm), w its strength (1 raging, 0 cured).
float4 _TinCanStormRegion;

// How much storm there is at a depth into the disc (0 the wall's foot, 1 the centre): it rises steeply into the wall,
// stays through the bands, and clears in the eye.
half TinCanStormProfile(half depth)
{
    return (half)(smoothstep(0.0, 0.15, depth) * (1.0 - smoothstep(0.8, 0.95, depth)));
}

// The storm's weight at a world position, at every height: its profile times its strength; 0 outside it.
half TinCanStormWeightAt(float3 positionWS)
{
    if (_TinCanStormRegion.z <= 0.0) return 0.0h;
    half depth = (half)saturate(1.0 - distance(positionWS.xz, _TinCanStormRegion.xy) / _TinCanStormRegion.z);
    return TinCanStormProfile(depth) * (half)_TinCanStormRegion.w;
}

// The lightning (StormShaderGlobals.ApplyFlash): xyz where the strike is, w how far its glow reaches (0: no flash now).
float4 _TinCanStormFlash;
// rgb the glow's colour times how bright the flash is at this moment.
float4 _TinCanStormFlashColour;

// The strike lighting the cloud round it from inside, so a storm seen from afar flickers. Added after the night shade, so
// it shows as bright at night.
half3 TinCanStormGlow(half3 colour, float3 positionWS)
{
    if (_TinCanStormFlash.w <= 0.0) return colour;
    half near = (half)saturate(1.0 - distance(positionWS, _TinCanStormFlash.xyz) / _TinCanStormFlash.w);
    return colour + (half3)_TinCanStormFlashColour.rgb * (near * near);
}

#endif
