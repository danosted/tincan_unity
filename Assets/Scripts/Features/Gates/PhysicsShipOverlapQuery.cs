#nullable enable
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.Gates
{
    /// <summary>
    /// Reads the ship's solid colliders (hull, deck, fixtures under it); triggers do not count. The same check as the sky
    /// hazards' contact, kept here so gates do not depend on hazards.
    /// </summary>
    public sealed class PhysicsShipOverlapQuery : IShipOverlapQuery
    {
        private readonly Collider[] _hits = new Collider[64];

        public bool Overlaps(IAirshipView ship, Vector3 point, float radius)
        {
            var root = (ship as Component)?.transform ?? ship.Transform;
            if (root == null) return false;

            int count = Physics.OverlapSphereNonAlloc(point, radius, _hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(root)) return true;
            }
            return false;
        }
    }
}
