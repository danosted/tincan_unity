#nullable enable
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// The cloud sea as a height field, from a seed: a surface with slow swells, holes where low-frequency cover falls
    /// under a threshold (their rims roll down with a rounded shoulder), and lumps on top in one of three styles. Every
    /// sea approach in the cloud lab (<see cref="CloudShapes"/>) reads the same field, so they share holes and swells and
    /// compare fairly. Plan: .docs/plans/look-slice.md ("The cloud lab").
    /// </summary>
    public sealed class CloudSeaField
    {
        public enum LumpStyle
        {
            /// <summary>No lumps: the swells alone.</summary>
            None,
            /// <summary>Billow noise: rounded tops, creased valleys, three octaves.</summary>
            Billow,
            /// <summary>Domes on a jittered grid, blended by a smooth maximum: puffs with no seams between them.</summary>
            Domes,
        }

        private readonly int _seed;
        private readonly Vector2 _offset;

        public CloudSeaField(int seed, float surface, float floor, float cell, float holes, LumpStyle lumps)
        {
            _seed = seed;
            _offset = new Vector2(Hash(seed, 0, 0) * 500f, Hash(seed, 0, 1) * 500f);
            Surface = surface;
            Floor = floor;
            Cell = cell;
            Holes = holes;
            Lumps = lumps;
        }

        /// <summary>Where the sea's tops sit on average (m).</summary>
        public float Surface { get; }
        /// <summary>Below this the sea is cut away: the bottom of a hole (m).</summary>
        public float Floor { get; }
        /// <summary>The size of a lump (m).</summary>
        public float Cell { get; }
        /// <summary>Cover under this opens a hole, 0..1.</summary>
        public float Holes { get; }
        public LumpStyle Lumps { get; }
        /// <summary>The highest the lumps reach, for the height in the cloud the shader darkens the base by.</summary>
        public float Top => Surface + Cell * 0.6f;

        /// <summary>1 in cloud, 0 in a hole, easing across the rim.</summary>
        public float Openness(float x, float z) =>
            SmoothStep(Holes, Holes + 0.1f, Mathf.PerlinNoise(_offset.x + x * 0.0012f, _offset.y + z * 0.0012f));

        /// <summary>The sea's height without lumps: swells and holes. Its slope is the cloud's shape normal.</summary>
        public float ShapeHeight(float x, float z) => WithHoles(x, z, Swell(x, z));

        /// <summary>The sea's height with its lumps.</summary>
        public float Height(float x, float z) => WithHoles(x, z, Swell(x, z) + Lump(x, z));

        public Vector3 ShapeNormal(float x, float z, float step) => Normal(ShapeHeight, x, z, step);

        public Vector3 SurfaceNormal(float x, float z, float step) => Normal(Height, x, z, step);

        private float Swell(float x, float z) =>
            Surface + (Mathf.PerlinNoise(_offset.y + x * 0.003f, _offset.x + z * 0.003f) - 0.5f) * Cell * 0.5f;

        private float WithHoles(float x, float z, float height)
        {
            float open = 1f - Openness(x, z);
            // A rounded shoulder: the rim stays near the top, then falls away.
            return Mathf.Lerp(Floor - Cell * 0.5f, height, 1f - open * open);
        }

        private float Lump(float x, float z) => Lumps switch
        {
            LumpStyle.Billow => Billow(x, z),
            LumpStyle.Domes => Domes(x, z),
            _ => 0f,
        };

        private float Billow(float x, float z)
        {
            float sum = 0f, amplitude = Cell * 0.3f, frequency = 1f / (Cell * 1.2f);
            for (int octave = 0; octave < 3; octave++)
            {
                float n = Mathf.PerlinNoise(_offset.x * 1.7f + x * frequency, _offset.y * 1.3f + z * frequency);
                // Perlin rarely leaves 0.3..0.7; stretched, so the folds reach full height.
                sum += amplitude * Mathf.Clamp01(Mathf.Abs(2f * n - 1f) * 2.5f);
                amplitude *= 0.5f;
                frequency *= 2.1f;
            }

            return sum - Cell * 0.25f;
        }

        private float Domes(float x, float z)
        {
            int cx = Mathf.FloorToInt(x / Cell), cz = Mathf.FloorToInt(z / Cell);
            float height = 0f;
            bool first = true;
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int ix = cx + dx, iz = cz + dz;
                var centre = new Vector2((ix + 0.2f + 0.6f * CellHash(ix, iz, 0)) * Cell, (iz + 0.2f + 0.6f * CellHash(ix, iz, 1)) * Cell);
                float radius = Cell * Mathf.Lerp(0.55f, 1f, CellHash(ix, iz, 2));
                float distance = Vector2.Distance(new Vector2(x, z), centre);
                // A sphere's cap, sunk so a quarter of its radius shows; beyond its edge it keeps falling.
                float cap = (distance < radius ? Mathf.Sqrt(radius * radius - distance * distance) : radius - distance) - radius * 0.75f;
                height = first ? cap : SmoothMax(height, cap, Cell * 0.2f);
                first = false;
            }

            return height;
        }

        private static Vector3 Normal(System.Func<float, float, float> height, float x, float z, float step) =>
            new Vector3(height(x - step, z) - height(x + step, z), 2f * step, height(x, z - step) - height(x, z + step)).normalized;

        private static float SmoothMax(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (a - b) / k);
            return Mathf.Lerp(b, a, h) + k * h * (1f - h);
        }

        private static float SmoothStep(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        private float CellHash(int x, int z, int salt) => Hash(x * 7919 + _seed, z, salt);

        private static float Hash(int x, int z, int salt) => Hash((uint)x, (uint)z, (uint)salt);

        private static float Hash(uint x, uint z, uint salt)
        {
            uint h = x * 0x8da6b343u ^ z * 0xd8163841u ^ salt * 0xcb1ab31fu;
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
