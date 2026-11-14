#nullable enable
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
        /// rim; a hole that is only partly open cuts at most its openness.</summary>
        public static float HoleCut(float x, float z, IReadOnlyList<CloudHoleShape> holes)
        {
            float cut = 0f;
            for (int i = 0; i < holes.Count; i++)
            {
                CloudHoleShape hole = holes[i];
                if (hole.Openness <= 0f) continue;

                float distance = Vector2.Distance(new Vector2(x, z), hole.Centre);
                float inside = 1f - SmoothStep(hole.Radius, hole.Radius + hole.Rim, distance);
                cut = Mathf.Max(cut, hole.Openness * inside);
            }

            return cut;
        }

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
