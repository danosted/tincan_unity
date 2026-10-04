#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>How a design's totals become flight: speed from thrust over mass, turning slowed by mass.</summary>
    [Serializable]
    public struct ShipStatsTuning
    {
        [Tooltip("Top speed (m/s) per unit of thrust over mass. The starter (thrust 400, mass about 600) flies at about 15 m/s.")]
        [Min(0f)] public float SpeedPerThrustOverMass;
        [Tooltip("No design flies faster than this (m/s).")]
        [Min(0f)] public float MaxSpeed;
        [Tooltip("Turn rate (deg/s) of a ship of ReferenceMass.")]
        [Min(0f)] public float BaseTurnSpeed;
        [Tooltip("Turn rate scales with ReferenceMass / mass, within TurnScaleRange.")]
        [Min(1f)] public float ReferenceMass;
        public Vector2 TurnScaleRange;
        [Tooltip("Health of a ship whose parts add none.")]
        [Min(1f)] public float MinHealth;

        public static ShipStatsTuning Default => new()
        {
            SpeedPerThrustOverMass = 24f,
            MaxSpeed = 30f,
            BaseTurnSpeed = 45f,
            ReferenceMass = 600f,
            TurnScaleRange = new Vector2(0.3f, 1.5f),
            MinHealth = 100f,
        };
    }
}
