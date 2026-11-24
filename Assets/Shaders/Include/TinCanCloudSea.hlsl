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
float4 _TinCanCloudWeb;       // x cell size, y rift width, z rim, w ragged (CloudWeb)
float4 _TinCanCloudWebMotion; // x open share (0: no web), y drift per hour, zw seed offset

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

// The web of rifts (plan .docs/plans/cloud-sea-fog.md, F3): CloudSeaFormula.WebCut, which the game reads. Plain hashes,
// no sin in them, so C# and the GPU agree.
float2 TinCanCloudWebHash(float2 p)
{
    float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.xx + q.yz) * q.zy);
}

float TinCanCloudWebNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(TinCanCloudWebHash(cell).x, TinCanCloudWebHash(cell + float2(1, 0)).x, f.x),
                lerp(TinCanCloudWebHash(cell + float2(0, 1)).x, TinCanCloudWebHash(cell + 1.0).x, f.x), f.y);
}

// How far xz is inside a rift of the web, 0..1. The sea is cut into cells about cell metres across, like the plates of
// a cracked crust; their borders, made ragged, are the rifts, width metres wide (wandering along them), rolling shut
// over rim metres. Not every border is open: each one opens and closes on its own slow cycle, about openShare of them
// open at a time, so neighbouring cells join into larger cloud islands. The cells' centres drift too; drift is in
// cycles per hour, so the web changes over many minutes.
float TinCanCloudWebCutAt(float2 xz, float time, float cell, float width, float rim, float ragged, float openShare,
    float drift, float2 offset)
{
    if (openShare <= 0.0 || cell <= 1.0) return 0.0;

    float2 p = (xz + offset) / cell;
    float2 warp = float2(TinCanCloudWebNoise(p * 1.7 + 3.1), TinCanCloudWebNoise(p * 1.7 + 11.7)) - 0.5;
    warp += 0.5 * (float2(TinCanCloudWebNoise(p * 4.3 + 5.3), TinCanCloudWebNoise(p * 4.3 + 19.1)) - 0.5);
    p += warp * ragged;

    float turn = time * drift * (2.0 * TINCAN_PI / 3600.0);
    float2 base = floor(p);
    float2 sites[9];
    float2 ids[9];
    int nearest = 0;
    float nearestSq = 1e9;
    for (int k = 0; k < 9; k++)
    {
        float2 id = base + float2(k % 3 - 1, k / 3 - 1);
        float2 h = TinCanCloudWebHash(id);
        ids[k] = id;
        sites[k] = id + 0.5 + 0.38 * float2(sin(turn + 6.2832 * h.x), cos(turn * 0.83 + 6.2832 * h.y));
        float d = dot(sites[k] - p, sites[k] - p);
        if (d < nearestSq)
        {
            nearestSq = d;
            nearest = k;
        }
    }

    // Every border of the cell this point is in: metres to it (exact for a straight border), and whether it is open on
    // its own slow cycle (the same for both cells that share it). The strongest cut wins, so where three cells meet
    // nothing switches from one border to another.
    float halfWidth = 0.5 * width * (0.5 + TinCanCloudWebNoise(p * 3.0 + 7.7));
    float2 own = sites[nearest];
    float2 ownId = ids[nearest];
    float cut = 0.0;
    for (int n = 0; n < 9; n++)
    {
        if (n == nearest) continue;
        float2 other = sites[n];
        float border = (dot(other - p, other - p) - nearestSq) / max(2.0 * length(other - own), 1e-4) * cell;
        float key = TinCanCloudWebHash(ownId + ids[n] + 0.37 * abs(ownId - ids[n])).x;
        float cycle = 0.5 + 0.5 * sin(turn * 0.5 + 6.2832 * key);
        float open = smoothstep(1.0 - openShare - 0.12, 1.0 - openShare + 0.12, cycle);
        cut = max(cut, open * (1.0 - smoothstep(halfWidth, halfWidth + max(rim, 1e-3), border)));
    }

    return cut;
}

float TinCanCloudSwell(float2 xz)
{
    return TinCanCloudSwellAt(xz, _TinCanCloudSea.x, _TinCanCloudSea.z, _TinCanCloudSea.w, _TinCanCloudSwell.x, _TinCanCloudSwell.yz);
}

// How far xz is inside a rift of the web now, 0..1.
float TinCanCloudWebCut(float2 xz)
{
    return TinCanCloudWebCutAt(xz, _TinCanCloudSea.x, _TinCanCloudWeb.x, _TinCanCloudWeb.y, _TinCanCloudWeb.z,
        _TinCanCloudWeb.w, _TinCanCloudWebMotion.x, _TinCanCloudWebMotion.y, _TinCanCloudWebMotion.zw);
}

// How far xz is inside the holes or the web's rifts now, 0..1 (CloudSurfaceQuery's cut).
float TinCanCloudHoleCut(float2 xz)
{
    float cut = TinCanCloudWebCut(xz);
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
