#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// One cave rock on an island's top, in island space: a hollow dome of rock with a mouth. <see cref="Base"/> is the
    /// middle of the dome's foot, at or under the ground all round the wall; <see cref="Floor"/> is the ground in the middle
    /// of the chamber. <see cref="Facing"/> is the mouth's direction from the middle (radians, from +X toward +Z).
    /// </summary>
    public readonly struct SkyIslandCave
    {
        public readonly Vector3 Base;
        public readonly float Floor;
        public readonly float Radius;
        public readonly float Height;
        public readonly float Wall;
        public readonly float Facing;
        public readonly uint Seed;

        public SkyIslandCave(Vector3 @base, float floor, float radius, float height, float wall, float facing, uint seed)
        {
            Base = @base;
            Floor = floor;
            Radius = radius;
            Height = height;
            Wall = wall;
            Facing = facing;
            Seed = seed;
        }

        /// <summary>The chamber's middle on its floor: open ground, room for a campfire and a tent.</summary>
        public Vector3 Chamber => new(Base.x, Floor, Base.z);

        /// <summary>How far the rock reaches from the middle, level.</summary>
        public float OuterRadius => Radius + Wall;

        /// <summary>The mouth's direction, level.</summary>
        public Vector3 MouthDirection => new(Mathf.Cos(Facing), 0f, Mathf.Sin(Facing));

        public override string ToString() => $"cave at {Base} r{Radius:0.0} h{Height:0.0}";
    }
}
