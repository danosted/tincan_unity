// The cloud sea's shape (plan .docs/plans/cloud-sea.md, "The drawn sea and its holes"): the same formula as
// Features/CloudBoundary/Sea/CloudSeaFormula.cs, which the game reads. Keep the two in step;
// CloudSeaFormulaParityTests runs both on the same points. Plain HLSL with no Unity includes, so a compute shader can
// test it. The globals come from CloudSeaShaderGlobals (CloudSeaPresenter), each frame.
#ifndef TINCAN_CLOUD_SEA_INCLUDED
#define TINCAN_CLOUD_SEA_INCLUDED

#define TINCAN_CLOUD_MAX_HOLES 16
#define TINCAN_PI 3.14159265359

float4 _TinCanCloudSea;   // x shared time, y base altitude, z swell amplitude, w swell wavelength
float4 _TinCanCloudSwell; // x swell speed, yz seed offset, w where the drawn surface has faded out (m from the camera)
float4 _TinCanCloudHoles[TINCAN_CLOUD_MAX_HOLES]; // xy centre, z radius now, w rim
float _TinCanCloudHoleOpenness[TINCAN_CLOUD_MAX_HOLES];
float4 _TinCanCloudHoleReach[TINCAN_CLOUD_MAX_HOLES]; // xy from the centre to one end of a rift (zero: a round hole), z how ragged its edge is
float _TinCanCloudHoleCount;

// The swell at xz: -1..1 times the amplitude. Three long, slow waves at different angles.
float TinCanCloudSwellAt(float2 xz, float time, float amplitude, float wavelength, float speed, float2 offset)
{
    if (amplitude <= 0.0) return 0.0;

    const float2 directions[3] = { float2(0.958, 0.287), float2(-0.371, 0.928), float2(0.753, -0.659) };
    const float lengths[3] = { 1.0, 0.61, 0.37 };
    const float weights[3] = { 0.55, 0.3, 0.15 };
    float sum = 0.0;
    [unroll] for (int i = 0; i < 3; i++)
    {
        float number = 2.0 * TINCAN_PI / (max(wavelength, 1.0) * lengths[i]);
        float along = dot(directions[i], xz + offset);
        sum += weights[i] * sin(number * (along - speed * time));
    }

    return sum * amplitude;
}

// How much wider (above 1) or narrower a rift is at along (-1 .. 1): CloudSeaFormula.RaggedWidth.
float TinCanCloudRaggedWidth(float along, float phase, float ragged)
{
    if (ragged <= 0.0) return 1.0;

    float wave = 0.5 * sin(along * 7.3 + phase * 3.0) + 0.3 * sin(along * 17.9 + phase * 5.0)
        + 0.2 * sin(along * 41.3 + phase * 7.0);
    return max(1.0 + ragged * wave, 0.25);
}

// A hole's radius across from xz (a ragged rift's wanders along it), and the distance to its centre, or for a rift
// (reach.xy not zero) to the nearest point on its line: CloudSeaFormula.EdgeRadius.
float TinCanCloudHoleEdge(float2 xz, float4 hole, float3 reach, out float dist)
{
    float reachSq = dot(reach.xy, reach.xy);
    if (reachSq <= 1e-6)
    {
        dist = distance(xz, hole.xy);
        return hole.z;
    }

    float along = clamp(dot(xz - hole.xy, reach.xy) / reachSq, -1.0, 1.0);
    dist = distance(xz, hole.xy + reach.xy * along);
    return hole.z * TinCanCloudRaggedWidth(along, atan2(reach.y, reach.x), reach.z);
}

// How far xz is inside one hole: its openness in the open part, rolling to 0 across the rim.
float TinCanCloudHoleCutOne(float2 xz, float4 hole, float openness, float3 reach)
{
    if (openness <= 0.0) return 0.0;

    float dist;
    float radius = TinCanCloudHoleEdge(xz, hole, reach, dist);
    float inside = 1.0 - smoothstep(radius, radius + max(hole.w, 1e-5), dist);
    return openness * inside;
}

float TinCanCloudSwell(float2 xz)
{
    return TinCanCloudSwellAt(xz, _TinCanCloudSea.x, _TinCanCloudSea.z, _TinCanCloudSea.w, _TinCanCloudSwell.x, _TinCanCloudSwell.yz);
}

// How far xz is inside the holes now, 0..1.
float TinCanCloudHoleCut(float2 xz)
{
    float cut = 0.0;
    int count = (int)_TinCanCloudHoleCount;
    for (int i = 0; i < TINCAN_CLOUD_MAX_HOLES; i++)
    {
        if (i >= count) break;
        cut = max(cut, TinCanCloudHoleCutOne(xz, _TinCanCloudHoles[i], _TinCanCloudHoleOpenness[i], _TinCanCloudHoleReach[i].xyz));
    }

    return cut;
}

// Visual only (never read by the game): a raised, rounded lip just outside each hole's rim, growing as it opens.
float TinCanCloudHoleLip(float2 xz)
{
    float lip = 0.0;
    int count = (int)_TinCanCloudHoleCount;
    for (int i = 0; i < TINCAN_CLOUD_MAX_HOLES; i++)
    {
        if (i >= count) break;
        float4 hole = _TinCanCloudHoles[i];
        float rim = max(hole.w, 1.0);
        float dist;
        float radius = TinCanCloudHoleEdge(xz, hole, _TinCanCloudHoleReach[i].xyz, dist);
        float offset = (dist - (radius + rim)) / (rim * 0.7);
        lip = max(lip, _TinCanCloudHoleOpenness[i] * exp(-offset * offset));
    }

    return lip;
}

#endif
