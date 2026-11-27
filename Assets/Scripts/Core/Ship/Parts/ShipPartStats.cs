#nullable enable
using System;
using UnityEngine;

namespace TinCan.Core.Ship.Parts
{
    /// <summary>
    /// What a part adds to a ship: its hull is the ship's health. Mass, lift and thrust are counted but do not change how
    /// the ship flies for now: its core part's handling profile does (plan .docs/plans/ship-handling-profiles.md).
    /// </summary>
    [Serializable]
    public struct ShipPartStats
    {
        [Min(0f)] public float Mass;
        [Tooltip("Mass this part holds up.")]
        [Min(0f)] public float Lift;
        [Min(0f)] public float Thrust;
        [Tooltip("Health this part adds to the ship.")]
        [Min(0f)] public float Hull;

        public ShipPartStats(float mass, float lift = 0f, float thrust = 0f, float hull = 0f)
        {
            Mass = mass;
            Lift = lift;
            Thrust = thrust;
            Hull = hull;
        }
    }
}
