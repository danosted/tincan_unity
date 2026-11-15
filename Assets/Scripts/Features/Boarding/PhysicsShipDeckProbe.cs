#nullable enable
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.Boarding
{
    /// <summary>
    /// <see cref="IShipDeckProbe"/> by physics: a ray from just above the point straight down finds a solid collider of
    /// the ship itself within <see cref="Reach"/>.
    /// </summary>
    public sealed class PhysicsShipDeckProbe : IShipDeckProbe
    {
        /// <summary>How far below the point the deck may be, in metres.</summary>
        public const float Reach = 6f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        public bool HasDeckUnder(IAirshipView ship, Vector3 position)
        {
            int count = Physics.RaycastNonAlloc(position + Vector3.up, Vector3.down, _hits, Reach + 1f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(ship.Transform)) return true;
            }

            return false;
        }
    }
}
