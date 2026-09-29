#nullable enable
using System.Collections.Generic;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>The ship's solid body, as hazards see it. A seam over physics, so the use case stays testable.</summary>
    public interface IShipContactQuery
    {
        /// <summary>Where a hazard homes: the middle of the ship's solid colliders. The pivot can sit metres off the hull.</summary>
        Vector3 Center(IAirshipView ship);

        /// <summary>Does a sphere at this point touch the ship?</summary>
        bool Touches(IAirshipView ship, Vector3 point, float radius);
    }

    /// <summary>
    /// Reads the ship's solid colliders: the hull, deck or a fixture parented under it. Triggers (interaction volumes)
    /// do not count, and players are not parented to the ship, so standing on the deck is not contact.
    /// </summary>
    public sealed class PhysicsShipContactQuery : IShipContactQuery
    {
        private readonly Collider[] _hits = new Collider[32];
        private readonly List<Collider> _shipColliders = new();

        public Vector3 Center(IAirshipView ship)
        {
            var root = Root(ship);
            if (root == null) return ship.Transform.position;

            root.GetComponentsInChildren(_shipColliders);
            Bounds? body = null;
            foreach (var collider in _shipColliders)
            {
                if (collider.isTrigger || !collider.enabled) continue;
                if (body is { } bounds)
                {
                    bounds.Encapsulate(collider.bounds);
                    body = bounds;
                }
                else body = collider.bounds;
            }
            return body?.center ?? root.position;
        }

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
