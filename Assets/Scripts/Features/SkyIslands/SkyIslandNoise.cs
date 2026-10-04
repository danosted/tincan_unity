#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Smooth 3D value noise from integer hashes, in [-1, 1], plus a few octaves of it. Shapes island edges and crags.
    /// </summary>
    public static class SkyIslandNoise
    {
        public static float Value(uint seed, float x, float y, float z)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), z0 = Mathf.FloorToInt(z);
            float tx = Smooth(x - x0), ty = Smooth(y - y0), tz = Smooth(z - z0);

            float c000 = Corner(seed, x0, y0, z0), c100 = Corner(seed, x0 + 1, y0, z0);
            float c010 = Corner(seed, x0, y0 + 1, z0), c110 = Corner(seed, x0 + 1, y0 + 1, z0);
            float c001 = Corner(seed, x0, y0, z0 + 1), c101 = Corner(seed, x0 + 1, y0, z0 + 1);
            float c011 = Corner(seed, x0, y0 + 1, z0 + 1), c111 = Corner(seed, x0 + 1, y0 + 1, z0 + 1);

            float x00 = Mathf.Lerp(c000, c100, tx), x10 = Mathf.Lerp(c010, c110, tx);
            float x01 = Mathf.Lerp(c001, c101, tx), x11 = Mathf.Lerp(c011, c111, tx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, ty), Mathf.Lerp(x01, x11, ty), tz);
        }

        /// <summary>Octaves of <see cref="Value"/>, each twice as fine and half as strong; stays in [-1, 1].</summary>
        public static float Fractal(uint seed, float x, float y, float z, int octaves)
        {
            float sum = 0f, amplitude = 1f, total = 0f, frequency = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(seed + (uint)i * 7919u, x * frequency, y * frequency, z * frequency) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return total > 0f ? sum / total : 0f;
        }

        private static float Corner(uint seed, int x, int y, int z) =>
            new SkyIslandRandom(SkyIslandRandom.Hash((int)seed, x, y, z)).Value() * 2f - 1f;

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
