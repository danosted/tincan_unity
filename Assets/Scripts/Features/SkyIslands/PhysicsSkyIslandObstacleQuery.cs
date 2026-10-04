#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Island rock as an obstacle: a sphere overlapping any island piece (<see cref="SkyIslandBody"/>) is blocked. Open
    /// sky (no island near, as the builder knows) costs no query.
    /// </summary>
    public sealed class PhysicsSkyIslandObstacleQuery : IWorldObstacleQuery
    {
        private readonly Collider[] _hits = new Collider[32];
        private readonly ISkyIslandBuilder _islands;

        public PhysicsSkyIslandObstacleQuery(ISkyIslandBuilder islands) => _islands = islands;

        public bool IsBlocked(Vector3 point, float radius)
        {
            if (!_islands.AnyNear(point, radius)) return false;
            int count = Physics.OverlapSphereNonAlloc(point, radius, _hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].TryGetComponent<SkyIslandBody>(out _)) return true;
            }
            return false;
        }
    }
}
