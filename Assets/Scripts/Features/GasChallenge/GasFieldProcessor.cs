#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// Domain, pure: where a gas field's pockets go, and when the ship has left one behind. Pockets fill a band ahead of
    /// the ship along its heading (level with the horizon), one per lane across the band so a crew has to pick a gap,
    /// with a random offset within each lane, along the heading and in height.
    /// </summary>
    public sealed class GasFieldProcessor
    {
        /// <summary>The level heading from a velocity (when moving) or else the bow's direction.</summary>
        public Vector3 Heading(Vector3 velocity, Vector3 forward)
        {
            var level = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (level.sqrMagnitude < 1f) level = Vector3.ProjectOnPlane(forward, Vector3.up);
            return level.sqrMagnitude < 1e-6f ? Vector3.forward : level.normalized;
        }

        /// <summary>
        /// <paramref name="count"/> spots: lane i of <paramref name="count"/> across <paramref name="spread"/> metres either side,
        /// <paramref name="ahead"/> metres ahead give or take half of <paramref name="depth"/>, and within half of
        /// <paramref name="height"/> of the ship's altitude. <paramref name="blocked"/> rejects a spot (rock), which is tried
        /// again up to <paramref name="tries"/> times, then dropped.
        /// </summary>
        public List<Vector3> Spots(Vector3 shipPosition, Vector3 heading, int count, float ahead, float spread, float depth,
            float height, System.Random random, Func<Vector3, bool> blocked, int tries)
        {
            var spots = new List<Vector3>(Math.Max(count, 0));
            var right = Vector3.Cross(Vector3.up, heading).normalized;
            float lane = count > 0 ? 2f * spread / count : 0f;

            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < Math.Max(tries, 1); attempt++)
                {
                    float across = -spread + lane * (i + (float)random.NextDouble());
                    float along = ahead + depth * ((float)random.NextDouble() - 0.5f);
                    float up = height * ((float)random.NextDouble() - 0.5f);
                    var spot = shipPosition + heading * along + right * across + Vector3.up * up;
                    if (blocked(spot)) continue;

                    spots.Add(spot);
                    break;
                }
            }

            return spots;
        }

        /// <summary>The ship has left the pocket more than <paramref name="passedDistance"/> metres behind, along its heading.</summary>
        public bool IsPassed(Vector3 pocket, Vector3 shipPosition, Vector3 heading, float passedDistance) =>
            Vector3.Dot(pocket - shipPosition, heading) < -passedDistance;
    }
}
