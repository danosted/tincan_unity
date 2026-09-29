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
    /// Domain, pure: turns the occupant's aim into barrel angles and the muzzle direction. The occupant stays in their
    /// body, so the aim is the body's facing plus its look pitch (Unity pitch: positive looks down); the barrel follows it
    /// within the limits, measured in the cannon base's own frame so it turns with the ship.
    /// </summary>
    public class CannonAimProcessor
    {
        /// <summary>Barrel yaw and elevation (degrees, base frame) for an occupant facing <paramref name="bodyForward"/>.</summary>
        public (float Yaw, float Elevation) BarrelAngles(Quaternion baseRotation, Vector3 bodyForward, float lookPitch, in CannonAimLimits limits)
        {
            Vector3 up = baseRotation * Vector3.up;
            Vector3 flat = Vector3.ProjectOnPlane(bodyForward, up);
            float yaw = flat.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(baseRotation * Vector3.forward, flat, up) : 0f;

            return (Mathf.Clamp(yaw, -limits.YawLimit, limits.YawLimit),
                Mathf.Clamp(-lookPitch, limits.MinElevation, limits.MaxElevation));
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
