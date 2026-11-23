#nullable enable
using UnityEngine;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Pure storm rules for the look and the sound. Depth (0 outside the wall, 1 at the eye's edge) times strength (1
    /// raging, 0 cured) is the weight that blends everything. Lightning falls in time slots: whether a slot has a strike,
    /// and when in it, come from a hash of the seed and the slot, so every peer on the same clock sees the same strikes.
    /// </summary>
    public static class StormProcessor
    {
        /// <summary>Each slot holds at most one strike.</summary>
        public const float SlotSeconds = 1.5f;

        public static float Weight(float depth, float strength) => Mathf.Clamp01(depth) * Mathf.Clamp01(strength);

        /// <summary>The chance a slot holds a strike at <paramref name="weight"/>: the rate, scaled from the threshold up.</summary>
        public static float StrikeChance(float weight, float strikesPerMinute, float minWeight)
        {
            if (weight <= minWeight) return 0f;
            float share = (weight - minWeight) / Mathf.Max(1e-4f, 1f - minWeight);
            return Mathf.Clamp01(strikesPerMinute / 60f * SlotSeconds * share);
        }

        /// <summary>When the strike in <paramref name="slot"/> falls (seconds into the slot), or null for none.</summary>
        public static float? StrikeIn(int slot, int seed, float chance)
        {
            if (chance <= 0f || Hash01(seed, slot, 0) >= chance) return null;
            return Hash01(seed, slot, 1) * SlotSeconds;
        }

        /// <summary>The flash at <paramref name="sinceStrike"/> seconds: two quick flickers fading over the flash.</summary>
        public static float Flash(float sinceStrike, float flashSeconds)
        {
            if (sinceStrike < 0f || sinceStrike > flashSeconds) return 0f;
            float t = sinceStrike / flashSeconds;
            float flicker = t < 0.15f ? 1f : t < 0.3f ? 0.35f : 0.8f;
            return flicker * (1f - t);
        }

        /// <summary>Seconds from flash to thunder: the deeper the storm, the nearer the strikes.</summary>
        public static float ThunderDelay(float weight, Vector2 range, int seed, int slot) =>
            Mathf.Lerp(range.y, range.x, Mathf.Clamp01(weight)) * Mathf.Lerp(0.7f, 1.3f, Hash01(seed, slot, 2));

        /// <summary>The heart's pulse volume share: silent below <paramref name="fromDepth"/>, full at the eye's edge.</summary>
        public static float HeartShare(float depth, float strength, float fromDepth)
        {
            float near = Mathf.InverseLerp(fromDepth, 1f, depth);
            return near * near * Mathf.Clamp01(strength);
        }

        /// <summary>A stable hash of (seed, slot, lane) in [0, 1).</summary>
        public static float Hash01(int seed, int slot, int lane)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)slot * 0x85EBCA77u ^ (uint)lane * 0xC2B2AE3Du;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
