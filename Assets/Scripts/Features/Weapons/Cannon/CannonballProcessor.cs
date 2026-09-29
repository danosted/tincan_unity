#nullable enable
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>When a cannonball stops flying on its own.</summary>
    public readonly struct CannonballLimits
    {
        public readonly float MaxLifetime;
        public readonly float MaxRange;
        /// <summary>World height below which the ball is gone (the cloud surface); null for none.</summary>
        public readonly float? FloorHeight;

        public CannonballLimits(float maxLifetime, float maxRange, float? floorHeight = null)
        {
            MaxLifetime = maxLifetime;
            MaxRange = maxRange;
            FloorHeight = floorHeight;
        }
    }

    /// <summary>
    /// Domain, pure: the piece of an arc to sweep on a tick and whether the ball has run out. The segment for tick k
    /// runs from the ball's position at k-1 to its position at k, so it ends where the ball is while the targets stand
    /// where they are on k: a moving target is hit only if it is really on that piece at that time. On the fire tick
    /// the segment is empty.
    /// </summary>
    public class CannonballProcessor
    {
        public (Vector3 From, Vector3 To) SegmentAt(in BallisticArc arc, int tick, int tickRate) =>
            (arc.PositionAt(arc.AgeAt(tick - 1, tickRate)), arc.PositionAt(arc.AgeAt(tick, tickRate)));

        /// <summary>True once the ball at <paramref name="tick"/> is past its lifetime, its range or below the floor.</summary>
        public bool IsSpent(in BallisticArc arc, int tick, int tickRate, in CannonballLimits limits)
        {
            float age = arc.AgeAt(tick, tickRate);
            if (age >= limits.MaxLifetime) return true;

            Vector3 position = arc.PositionAt(age);
            if ((position - arc.Origin).sqrMagnitude >= limits.MaxRange * limits.MaxRange) return true;
            return limits.FloorHeight is { } floor && position.y < floor;
        }
    }
}
