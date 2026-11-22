#nullable enable
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>The ground the player aims at: where, which way it faces, and whether it is part of a ship.</summary>
    public readonly struct CampGround
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly bool OnShip;

        public CampGround(Vector3 point, Vector3 normal, bool onShip)
        {
            Point = point;
            Normal = normal;
            OnShip = onShip;
        }
    }
}
