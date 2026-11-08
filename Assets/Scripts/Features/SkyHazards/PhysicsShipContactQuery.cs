#nullable enable
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>
    /// Reads the ship's solid colliders: the hull, deck or a fixture parented under it. Triggers (interaction volumes)
    /// do not count, and players are not parented to the ship, so standing on the deck is not contact.
    /// </summary>
    public sealed class PhysicsShipContactQuery : IShipContactQuery
    {
        private readonly Collider[] _hits = new Collider[32];

        public bool Touches(IAirshipView ship, Vector3 point, float radius)
        {
            var root = Root(ship);
            if (root == null) return false;

            int count = Physics.OverlapSphereNonAlloc(point, radius, _hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(root)) return true;
            }
            return false;
        }

        private static Transform? Root(IAirshipView ship) => (ship as Component)?.transform ?? ship.Transform;
    }
}
