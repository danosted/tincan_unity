#nullable enable
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// How far a cannon's barrel turns from its rest direction, in degrees; elevation is positive up. A yaw limit of 180
    /// or more means the barrel turns all the way round (a turret).
    /// </summary>
    public readonly struct CannonAimLimits
    {
        public const float FullTurn = 180f;

        public readonly float YawLimit;
        public readonly float MinElevation;
        public readonly float MaxElevation;

        public CannonAimLimits(float yawLimit, float minElevation, float maxElevation)
        {
            YawLimit = yawLimit;
            MinElevation = minElevation;
            MaxElevation = maxElevation;
        }

        /// <summary>The barrel turns all the way round; its yaw wraps instead of stopping.</summary>
        public bool TurnsFully => YawLimit >= FullTurn;
    }

    /// <summary>
    /// Domain, pure: the gunner's aim and what it does to the barrel. The aim is the station aim of the occupant's input
    /// (<c>HumanoidInputState.StationAim</c>): yaw and elevation in degrees in the cannon base's own frame, so it turns
    /// with the ship. It is steered by the Gunner context's Aim; the body's own look plays no part. A limited yaw stops
    /// at its limits; a full-turn yaw wraps into -180..180, so it never grows without bound.
    /// </summary>
    public class CannonAimProcessor
    {
        /// <summary>An aim (x yaw, y elevation) within the limits, as barrel angles.</summary>
        public (float Yaw, float Elevation) ClampAim(Vector2 aim, in CannonAimLimits limits)
        {
            float yaw = limits.TurnsFully ? Mathf.DeltaAngle(0f, aim.x) : Mathf.Clamp(aim.x, -limits.YawLimit, limits.YawLimit);
            return (yaw, Mathf.Clamp(aim.y, limits.MinElevation, limits.MaxElevation));
        }

        /// <summary>
        /// The aim after a look delta (mouse counts; x right, y up) at <paramref name="sensitivity"/> degrees per count,
        /// held within the limits: pushing past a limit leaves the aim on it, and turning back moves it at once. A
        /// full-turn yaw wraps.
        /// </summary>
        public Vector2 Steer(Vector2 aim, Vector2 delta, float sensitivity, in CannonAimLimits limits)
        {
            var (yaw, elevation) = ClampAim(aim + delta * sensitivity, limits);
            return new Vector2(yaw, elevation);
        }

        /// <summary>The barrel's local rotation for the angles, relative to the base.</summary>
        public Quaternion BarrelLocalRotation(float yaw, float elevation) => Quaternion.Euler(-elevation, yaw, 0f);

        /// <summary>The world direction the muzzle points in.</summary>
        public Vector3 MuzzleDirection(Quaternion baseRotation, float yaw, float elevation) =>
            baseRotation * BarrelLocalRotation(yaw, elevation) * Vector3.forward;
    }
}
