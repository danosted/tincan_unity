#nullable enable
using System;
using UnityEngine;

namespace TinCan.Core.Ship.Parts
{
    /// <summary>
    /// What a part adds to a ship. A ship's mass is carried by its lift (it flies only with lift at least its mass), its
    /// thrust drives it (speed grows with thrust over mass), and its hull is its health.
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
