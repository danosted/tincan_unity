#nullable enable
using System;
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// How a ship flies: the values <see cref="AirshipMovementUseCase"/> steers it with. A designer keeps them in a
    /// <see cref="ShipHandlingProfile"/> asset; the ship's core part picks one (plan .docs/plans/ship-handling-profiles.md).
    /// </summary>
    [Serializable]
    public struct ShipHandling
    {
        [Header("Speed")]
        [Tooltip("Top speed ahead at full throttle, m/s. A GAS attribute (boosts change it); this is its base.")]
        [Min(0f)] public float MaxForwardSpeed;
        [Tooltip("Top speed astern at full reverse, m/s.")]
        [Min(0f)] public float MaxBackwardSpeed;
        [Tooltip("How fast the engine gains speed toward the throttle's, m/s per second.")]
        [Min(0f)] public float AccelerationRate;
        [Tooltip("How fast the engine loses speed with the throttle let go or turned down, m/s per second.")]
        [Min(0f)] public float DecelerationRate;
        [Tooltip("How quickly the ship's motion follows its nose, per second. Low: it drifts wide through a turn.")]
        [Min(0f)] public float VelocityBlendRate;

        [Header("Turning")]
        [Tooltip("Turn rate at full rudder, degrees per second. A GAS attribute (boosts change it); this is its base.")]
        [Min(0f)] public float TurnSpeed;
        [Tooltip("How fast turning and pitching build up with the helm held, degrees per second per second.")]
        [Min(0f)] public float AngularAcceleration;
        [Tooltip("How fast turning and pitching die away with the helm let go, degrees per second per second.")]
        [Min(0f)] public float AngularDeceleration;

        [Header("Pitch")]
        [Tooltip("Pitch rate at full up or down, degrees per second.")]
        [Min(0f)] public float PitchSpeed;
        [Tooltip("How fast the nose eases back to level with no pitch input, per second of the angle off level.")]
        [Min(0f)] public float PitchLevelSpeed;

        [Header("Banking")]
        [Tooltip("How far the ship leans into a full turn at top speed, degrees.")]
        [Min(0f)] public float MaxBankAngle;
        [Tooltip("How quickly it leans to that angle, per second.")]
        [Min(0f)] public float BankSpeed;

        [Header("World")]
        [Tooltip("How much the world pitches this ship: the level the features set (the cloud sea's slope) and a stall's " +
                 "nose drop. 1: fully, 0: the nose keeps to the horizon.")]
        [Range(0f, 1f)] public float ExternalPitchInfluence;

        /// <summary>A ship with no profile flies on these.</summary>
        public static ShipHandling Default => new()
        {
            MaxForwardSpeed = 15f,
            MaxBackwardSpeed = 8f,
            AccelerationRate = 5f,
            DecelerationRate = 2f,
            VelocityBlendRate = 2f,
            TurnSpeed = 45f,
            AngularAcceleration = 15f,
            AngularDeceleration = 20f,
            PitchSpeed = 30f,
            PitchLevelSpeed = 0.2f,
            MaxBankAngle = 15f,
            BankSpeed = 2f,
            ExternalPitchInfluence = 1f,
        };
    }
}
