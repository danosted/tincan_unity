#nullable enable
using UnityEngine;

namespace TinCan.Features.IslandDetails
{
    /// <summary>
    /// One detail as the placement puts it: what it is, which of that kind's prefabs (<see cref="Variant"/>), where it stands
    /// on the ground in island space, its turn about the vertical (degrees) and its size against the prefab's.
    /// </summary>
    public readonly struct IslandDetail
    {
        public readonly IslandDetailKind Kind;
        public readonly int Variant;
        public readonly Vector3 Position;
        public readonly float Yaw;
        public readonly float Scale;

        public IslandDetail(IslandDetailKind kind, int variant, Vector3 position, float yaw, float scale)
        {
            Kind = kind;
            Variant = variant;
            Position = position;
            Yaw = yaw;
            Scale = scale;
        }

        public Quaternion Rotation => Quaternion.Euler(0f, Yaw, 0f);

        public override string ToString() => $"{Kind} {Variant} at {Position} x{Scale:0.00}";
    }
}
