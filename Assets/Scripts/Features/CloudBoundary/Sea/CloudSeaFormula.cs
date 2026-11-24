#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The cloud sea's shape, one formula with two readers (plan .docs/plans/cloud-sea.md, "The drawn sea and its holes"):
    /// the game (<see cref="CloudSurfaceQuery"/>) and the sea shader (Shaders/Include/TinCanCloudSea.hlsl, which must stay
    /// in step with this file; <c>CloudSeaFormulaParityTests</c> runs both on the same points). Only visual detail (the
    /// billow noise, the raised rim) lives in the shader alone, and it never decides lift or a fall.
    /// </summary>
    public static class CloudSeaFormula
    {
        // Three long, slow waves at different angles, so the swell never lines up into stripes.
        private static readonly Vector2[] Directions = { new(0.958f, 0.287f), new(-0.371f, 0.928f), new(0.753f, -0.659f) };
        private static readonly float[] Lengths = { 1f, 0.61f, 0.37f };
        private static readonly float[] Weights = { 0.55f, 0.3f, 0.15f };

        /// <summary>The swell at (x, z) at <paramref name="time"/> on the shared clock: -1..1 times the amplitude.</summary>
        public static float Swell(float x, float z, float time, in CloudSwell swell)
        {
            if (swell.Amplitude <= 0f) return 0f;

            float sum = 0f;
            for (int i = 0; i < Directions.Length; i++)
            {
                float number = 2f * Mathf.PI / (swell.Wavelength * Lengths[i]);
                float along = Directions[i].x * (x + swell.Offset.x) + Directions[i].y * (z + swell.Offset.y);
                sum += Weights[i] * Mathf.Sin(number * (along - swell.Speed * time));
            }

            return sum * swell.Amplitude;
        }

        /// <summary>How far (x, z) is inside the holes: 1 in the open part of a fully open hole, rolling to 0 across its
        /// rim; a hole that is only partly open cuts at most its openness. A rift measures from the nearest point on its
        /// line, and a ragged rift is wider and narrower along it.</summary>
        public static float HoleCut(float x, float z, IReadOnlyList<CloudHoleShape> holes)
        {
            float cut = 0f;
            for (int i = 0; i < holes.Count; i++)
            {
                CloudHoleShape hole = holes[i];
                if (hole.Openness <= 0f) continue;

                float radius = EdgeRadius(new Vector2(x, z), hole, out float distance);
                float inside = 1f - SmoothStep(radius, radius + hole.Rim, distance);
                cut = Mathf.Max(cut, hole.Openness * inside);
            }

            return cut;
        }

        /// <summary>From <paramref name="point"/> to the hole's centre, or for a rift to the nearest point on its line.</summary>
        public static float DistanceTo(Vector2 point, in CloudHoleShape hole)
        {
            EdgeRadius(point, hole, out float distance);
            return distance;
        }

        /// <summary>The hole's radius across from <paramref name="point"/>, and the distance to its centre or line: a rift's
        /// width wanders along it by its raggedness (<see cref="RaggedWidth"/>).</summary>
        public static float EdgeRadius(Vector2 point, in CloudHoleShape hole, out float distance)
        {
            Vector2 reach = hole.Reach;
            float length = reach.sqrMagnitude;
            if (length <= 1e-6f)
            {
                distance = Vector2.Distance(point, hole.Centre);
                return hole.Radius;
            }

            float along = Mathf.Clamp(Vector2.Dot(point - hole.Centre, reach) / length, -1f, 1f);
            distance = Vector2.Distance(point, hole.Centre + reach * along);
            return hole.Radius * RaggedWidth(along, Mathf.Atan2(reach.y, reach.x), hole.Ragged);
        }

        /// <summary>How much wider (above 1) or narrower a rift is at <paramref name="along"/> (-1 one end .. 1 the
        /// other): three waves of a few, several and many bumps, out of step for each rift by its angle
        /// (<paramref name="phase"/>), scaled by <paramref name="ragged"/>. Never under a quarter of its width.</summary>
        public static float RaggedWidth(float along, float phase, float ragged)
        {
            if (ragged <= 0f) return 1f;

            float wave = 0.5f * Mathf.Sin(along * 7.3f + phase * 3f) + 0.3f * Mathf.Sin(along * 17.9f + phase * 5f)
                + 0.2f * Mathf.Sin(along * 41.3f + phase * 7f);
            return Mathf.Max(1f + ragged * wave, 0.25f);
        }

        /// <summary>
        /// How far (x, z) is inside a rift of the web at <paramref name="time"/> on the shared clock, 0..1
        /// (TinCanCloudWebCutAt in the shader). The sea is cut into cells; their borders, made ragged, are the rifts;
        /// each border opens and closes on its own slow cycle, so cells join into larger cloud islands.
        /// </summary>
        public static float WebCut(float x, float z, float time, in CloudWeb web)
        {
            if (!web.IsOn) return 0f;

            Vector2 p = (new Vector2(x, z) + web.Offset) / web.Cell;
            Vector2 warp = new Vector2(WebNoise(p * 1.7f + new Vector2(3.1f, 3.1f)), WebNoise(p * 1.7f + new Vector2(11.7f, 11.7f))) - new Vector2(0.5f, 0.5f);
            warp += 0.5f * (new Vector2(WebNoise(p * 4.3f + new Vector2(5.3f, 5.3f)), WebNoise(p * 4.3f + new Vector2(19.1f, 19.1f))) - new Vector2(0.5f, 0.5f));
            p += warp * web.Ragged;

            float turn = time * web.Drift * (2f * Mathf.PI / 3600f);
            var basis = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
            Span<Vector2> sites = stackalloc Vector2[9];
            Span<Vector2> ids = stackalloc Vector2[9];
            int nearest = 0;
            float nearestSq = 1e9f;
            for (int k = 0; k < 9; k++)
            {
                Vector2 id = basis + new Vector2(k % 3 - 1, k / 3 - 1);
                Vector2 h = WebHash(id);
                ids[k] = id;
                sites[k] = id + new Vector2(0.5f, 0.5f)
                    + 0.38f * new Vector2(Mathf.Sin(turn + 6.2832f * h.x), Mathf.Cos(turn * 0.83f + 6.2832f * h.y));
                float d = (sites[k] - p).sqrMagnitude;
                if (d < nearestSq)
                {
                    nearestSq = d;
                    nearest = k;
                }
            }

            // Every border of the cell the point is in, the strongest cut winning, so where three cells meet nothing
            // switches from one border to another.
            float halfWidth = 0.5f * web.Width * (0.5f + WebNoise(p * 3f + new Vector2(7.7f, 7.7f)));
            Vector2 own = sites[nearest];
            Vector2 ownId = ids[nearest];
            float cut = 0f;
            for (int n = 0; n < 9; n++)
            {
                if (n == nearest) continue;
                Vector2 other = sites[n];
                float border = ((other - p).sqrMagnitude - nearestSq) / Mathf.Max(2f * (other - own).magnitude, 1e-4f) * web.Cell;
                Vector2 pair = ownId + ids[n] + 0.37f * new Vector2(Mathf.Abs(ownId.x - ids[n].x), Mathf.Abs(ownId.y - ids[n].y));
                float cycle = 0.5f + 0.5f * Mathf.Sin(turn * 0.5f + 6.2832f * WebHash(pair).x);
                float open = SmoothStep(1f - web.OpenShare - 0.12f, 1f - web.OpenShare + 0.12f, cycle);
                cut = Mathf.Max(cut, open * (1f - SmoothStep(halfWidth, halfWidth + Mathf.Max(web.Rim, 1e-3f), border)));
            }

            return cut;
        }

        // TinCanCloudWebHash: a hash with no sin in it, so C# and the GPU agree.
        private static Vector2 WebHash(Vector2 p)
        {
            var q = new Vector3(Frac(p.x * 0.1031f), Frac(p.y * 0.1030f), Frac(p.x * 0.0973f));
            float d = q.x * (q.y + 33.33f) + q.y * (q.z + 33.33f) + q.z * (q.x + 33.33f);
            q += new Vector3(d, d, d);
            return new Vector2(Frac((q.x + q.y) * q.z), Frac((q.x + q.z) * q.y));
        }

        // TinCanCloudWebNoise: smooth value noise on WebHash.
        private static float WebNoise(Vector2 p)
        {
            var cell = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
            Vector2 f = p - cell;
            f = new Vector2(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y));
            float a = Mathf.Lerp(WebHash(cell).x, WebHash(cell + new Vector2(1f, 0f)).x, f.x);
            float b = Mathf.Lerp(WebHash(cell + new Vector2(0f, 1f)).x, WebHash(cell + Vector2.one).x, f.x);
            return Mathf.Lerp(a, b, f.y);
        }

        private static float Frac(float value) => value - Mathf.Floor(value);

        /// <summary>The surface height: the base, the swell, and the drop into the holes.</summary>
        public static float Height(float x, float z, float time, float baseAltitude, in CloudSwell swell,
            IReadOnlyList<CloudHoleShape> holes, float holeDepth) =>
            baseAltitude + Swell(x, z, time, swell) - HoleCut(x, z, holes) * holeDepth;

        /// <summary>HLSL's smoothstep, so both readers ease the same way.</summary>
        public static float SmoothStep(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / Mathf.Max(to - from, 1e-5f));
            return t * t * (3f - 2f * t);
        }
    }
}
