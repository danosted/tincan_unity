#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Domain, pure: one island's character, rolled from its shape seed, in its own space with the middle of its top at
    /// the origin. The mesh is built from it (<see cref="SkyIslandMeshProcessor"/>), and anything standing on the top asks
    /// it for the ground (<see cref="Surface"/>): sites and details. A landmass rolls the same way, then gets real
    /// hills (more of them, higher) on top.
    /// </summary>
    public readonly struct SkyIslandShape
    {
        /// <summary>The top's rings reach this share of the edge; past it the ground rounds down to the rim.</summary>
        public const float TopReach = 0.93f;

        private readonly uint _seed;
        private readonly float _radius;
        private readonly float _edgeAmount;
        private readonly float _edgeScale;
        private readonly float _hill;
        private readonly float _hillScale;
        private readonly int _hillOctaves;
        private readonly float _taper;
        private readonly float _cragAmount;
        private readonly float _lobeAmount;
        private readonly float _ledgeAmount;
        private readonly float _tuckIn;
        private readonly int _spires;
        private readonly float _spireWidth;
        private readonly float _spire0, _spire1, _spire2;
        private readonly float _height1, _height2;

        public readonly float Lip;
        public readonly Vector2 ApexOffset;

        public SkyIslandShape(in SkyIslandSpec island)
        {
            var random = new SkyIslandRandom(island.ShapeSeed);
            _seed = island.ShapeSeed;
            _radius = island.Radius;
            _edgeAmount = random.Range(0.1f, 0.22f);
            _edgeScale = random.Range(1.1f, 2.2f);
            _hill = island.Radius * random.Range(0.03f, 0.08f);
            _taper = random.Range(0.6f, 1.3f);
            _cragAmount = random.Range(0.2f, 0.38f);
            _lobeAmount = random.Range(0.08f, 0.2f);
            _ledgeAmount = random.Range(0.04f, 0.12f);
            Lip = 0.5f + island.Radius * random.Range(0.02f, 0.05f);

            // Up to three spires: the deepest (the apex) and up to two shorter ones; between them the rock tucks in higher.
            _tuckIn = random.Range(1.8f, 2.8f);
            _spires = random.Range(1, 3);
            _spireWidth = random.Range(0.9f, 1.6f);
            _spire0 = random.Range(0f, 2f * Mathf.PI);
            _spire1 = _spire0 + random.Range(1.6f, 2.6f);
            _spire2 = _spire0 - random.Range(1.6f, 2.6f);
            _height1 = random.Range(0.7f, 0.92f);
            _height2 = random.Range(0.65f, 0.9f);
            ApexOffset = new Vector2(Mathf.Cos(_spire0), Mathf.Sin(_spire0)) * island.Radius * random.Range(0.1f, 0.3f);

            // Rolled last, so an ordinary island keeps the shape it had before landmasses.
            _hillScale = 2f / Mathf.Max(1f, island.Radius);
            _hillOctaves = 3;
            if (!island.IsLandmass) return;
            _hill = island.Radius * random.Range(0.05f, 0.08f);
            // Hills about a quarter of the radius apart, none finer than the collision's grid can follow.
            _hillScale = 3.5f / Mathf.Max(1f, island.Radius);
            _hillOctaves = 3;
            _edgeAmount *= 0.6f;
            Lip = 1.5f + island.Radius * random.Range(0.01f, 0.02f);
        }

        /// <summary>The top's edge at this angle: within [1 - edge amount, 1] of the radius, so the radius is the most.</summary>
        public float Edge(float angle)
        {
            float noise = SkyIslandNoise.Fractal(_seed, Mathf.Cos(angle) * _edgeScale + 11f, 0f, Mathf.Sin(angle) * _edgeScale + 11f, 3);
            return _radius * (1f - _edgeAmount * (0.5f + 0.5f * noise));
        }

        /// <summary>Rolling ground, highest near the middle, flattening toward the rim. <paramref name="reach"/> is the
        /// point's share of the way out to the edge.</summary>
        public float Height(float x, float z, float reach)
        {
            float noise = SkyIslandNoise.Fractal(_seed + 101u, x * _hillScale + 31f, 0f, z * _hillScale + 31f, _hillOctaves);
            float falloff = 1f - reach * reach;
            return (_hill * noise + _hill * 0.6f) * falloff;
        }

        /// <summary>How far out a point of the top is, as a share of the edge in its direction (0 the middle, 1 the rim).</summary>
        public float Reach(float x, float z)
        {
            float distance = Mathf.Sqrt(x * x + z * z);
            return distance < 1e-4f ? 0f : distance / Edge(Mathf.Atan2(z, x));
        }

        /// <summary>
        /// The height of the visual ground straight above or below a point of the top (island space), as the mesh draws
        /// it: the rolling top out to <see cref="TopReach"/>, then down to the rim's lip, and the lip beyond.
        /// </summary>
        public float Surface(float x, float z)
        {
            float reach = Reach(x, z);
            if (reach <= TopReach) return Height(x, z, reach);

            float scale = TopReach / reach;
            float inner = Height(x * scale, z * scale, TopReach);
            return Mathf.Lerp(inner, -Lip, Mathf.Clamp01((reach - TopReach) / (1f - TopReach)));
        }

        /// <summary>Craggy noise on the underside at this angle and depth, in [-1, 1].</summary>
        public float Crag(float angle, float down) =>
            SkyIslandNoise.Fractal(_seed + 211u, Mathf.Cos(angle) * 3.5f + 53f, down * 5f, Mathf.Sin(angle) * 3.5f + 53f, 3);

        /// <summary>
        /// The underside's radius: tapering with depth, crags growing in below the rim, never past the rim above.
        /// </summary>
        public float Under(float angle, float down, float crag, float ledge)
        {
            float edge = Edge(angle);
            float taper = Mathf.Pow(1f - down, _taper);
            float grow = Mathf.Min(1f, down * 4f);
            float lobe = SkyIslandNoise.Value(_seed + 307u,
                Mathf.Cos(angle) * 1.2f + 71f, down * 1.2f, Mathf.Sin(angle) * 1.2f + 71f);
            float rough = (1f + _cragAmount * grow * crag) * (1f + _lobeAmount * grow * lobe) * (1f + ledge * grow);
            return Mathf.Min(edge * 0.97f * taper * rough, edge * 0.98f);
        }

        /// <summary>
        /// How the rock drops at this angle: the exponent on the share of the way down. 1 under the deepest spire (a
        /// steady drop), up to <c>_tuckIn</c> between spires (it stays high and curves in late). Every angle still ends
        /// at the apex.
        /// </summary>
        public float Hang(float angle)
        {
            float spire = Bump(angle, _spire0);
            if (_spires >= 2) spire = Mathf.Max(spire, _height1 * Bump(angle, _spire1));
            if (_spires >= 3) spire = Mathf.Max(spire, _height2 * Bump(angle, _spire2));
            return Mathf.Lerp(_tuckIn, 1f, spire);
        }

        /// <summary>A ring stepping in or out, for ledges between the rock layers: within plus or minus the ledge amount.</summary>
        public float Ledge(int ring) =>
            _ledgeAmount * (new SkyIslandRandom(SkyIslandRandom.Hash((int)_seed, ring, 613)).Value() * 2f - 1f);

        /// <summary>A rock layer's tone, one per ring.</summary>
        public float Strata(int ring) => new SkyIslandRandom(SkyIslandRandom.Hash((int)_seed, ring, 977)).Value();

        /// <summary>1 at the spire's angle, falling smoothly to 0 one spire width away.</summary>
        private float Bump(float angle, float at)
        {
            float apart = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, at * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float t = Mathf.Clamp01(1f - apart / _spireWidth);
            return t * t * (3f - 2f * t);
        }
    }
}
