#nullable enable
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// Domain: a cannonball's path in world space, fixed when it is fired: p(t) = origin + v0·t + ½·g·t². The server
    /// sweeps it tick by tick and every peer draws it from the same values, so nothing is sent while it flies.
    /// </summary>
    public readonly struct BallisticArc
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Velocity;
        public readonly Vector3 Gravity;
        public readonly int FireTick;

        public BallisticArc(Vector3 origin, Vector3 velocity, Vector3 gravity, int fireTick)
        {
            Origin = origin;
            Velocity = velocity;
            Gravity = gravity;
            FireTick = fireTick;
        }

        /// <summary>
        /// A shot from a muzzle: <paramref name="muzzleSpeed"/> along <paramref name="direction"/> plus the velocity the
        /// muzzle itself has (the ship's point velocity), so a ball fired from a moving ship keeps the ship's motion.
        /// </summary>
        public static BallisticArc FromMuzzle(Vector3 origin, Vector3 direction, float muzzleSpeed, Vector3 inheritedVelocity, Vector3 gravity, int fireTick) =>
            new(origin, direction.normalized * muzzleSpeed + inheritedVelocity, gravity, fireTick);

        public Vector3 PositionAt(float seconds) => Origin + Velocity * seconds + 0.5f * seconds * seconds * Gravity;

        public Vector3 VelocityAt(float seconds) => Velocity + Gravity * seconds;

        /// <summary>Seconds since the fire tick at <paramref name="tick"/>; never negative.</summary>
        public float AgeAt(int tick, int tickRate) => Mathf.Max(0, tick - FireTick) / (float)tickRate;
    }
}
