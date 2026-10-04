#nullable enable
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The cursor's ray in the preview's local space, and what it hit there (a built part's collider), if anything.
    /// </summary>
    public readonly struct ShipyardRay
    {
        public ShipyardRay(Vector3 origin, Vector3 direction, bool hasHit = false, Vector3 hitPoint = default, Vector3 hitNormal = default)
        {
            Origin = origin;
            Direction = direction;
            HasHit = hasHit;
            HitPoint = hitPoint;
            HitNormal = hitNormal;
        }

        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public bool HasHit { get; }
        public Vector3 HitPoint { get; }
        public Vector3 HitNormal { get; }
    }
}
