#nullable enable
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>How far a cannon's barrel turns from its rest direction, in degrees; elevation is positive up.</summary>
    public readonly struct CannonAimLimits
    {
        public readonly float YawLimit;
        public readonly float MinElevation;
        public readonly float MaxElevation;

        public CannonAimLimits(float yawLimit, float minElevation, float maxElevation)
        {
            YawLimit = yawLimit;
            MinElevation = minElevation;
            MaxElevation = maxElevation;
        }
    }

    /// <summary>
    /// Domain, pure: the gunner's aim and what it does to the barrel. The aim is the station aim of the occupant's input
    /// (<c>HumanoidInputState.StationAim</c>): yaw and elevation in degrees in the cannon base's own frame, so it turns
    /// with the ship. It is steered by the Gunner context's Aim and never leaves the limits, so it cannot wrap; the body's
    /// own look plays no part.
    /// </summary>
    public class CannonAimProcessor
    {
        /// <summary>An aim (x yaw, y elevation) within the limits, as barrel angles.</summary>
        public (float Yaw, float Elevation) ClampAim(Vector2 aim, in CannonAimLimits limits) =>
            (Mathf.Clamp(aim.x, -limits.YawLimit, limits.YawLimit), Mathf.Clamp(aim.y, limits.MinElevation, limits.MaxElevation));

        /// <summary>
        /// The aim after a look delta (mouse counts; x right, y up) at <paramref name="sensitivity"/> degrees per count,
        /// held within the limits: pushing past a limit leaves the aim on it, and turning back moves it at once.
        /// </summary>
        public Vector2 Steer(Vector2 aim, Vector2 delta, float sensitivity, in CannonAimLimits limits)
        {
            var (yaw, elevation) = ClampAim(aim + delta * sensitivity, limits);
            return new Vector2(yaw, elevation);
        }

        /// <summary>The barrel's local rotation for the angles, relative to the base.</summary>
        public Quaternion BarrelLocalRotation(float yaw, float elevation) => Quaternion.Euler(-elevation, yaw, 0f);

        /// <summary>
        /// The velocity a ball inherits at <paramref name="point"/>: the motion of that point as if it were bolted to the
        /// cannon's base (the ship's travel and turn), from the base's previous and current pose. The barrel's own swing
        /// is left out: aiming must not throw the ball.
        /// </summary>
        public Vector3 BaseVelocityAt(Vector3 point, Vector3 previousBasePosition, Quaternion previousBaseRotation,
            Vector3 basePosition, Quaternion baseRotation, float deltaTime)
        {
            if (deltaTime <= 0f) return Vector3.zero;

            Vector3 local = Quaternion.Inverse(baseRotation) * (point - basePosition);
            Vector3 previous = previousBasePosition + previousBaseRotation * local;
            return (point - previous) / deltaTime;
        }

        /// <summary>The world direction the muzzle points in.</summary>
        public Vector3 MuzzleDirection(Quaternion baseRotation, float yaw, float elevation) =>
            baseRotation * BarrelLocalRotation(yaw, elevation) * Vector3.forward;
    }
}
