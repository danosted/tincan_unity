#nullable enable
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// The placing questions through physics. The aim takes the nearest solid surface along the ray (triggers and the
    /// player's own colliders aside); a surface belongs to a ship when anything above it is an airship. The room is a
    /// capsule standing on the point, lifted over the slope the ground may have (<see cref="CampPlacementProcessor.RoomLift"/>).
    /// </summary>
    public sealed class PhysicsCampPlacementQuery : ICampPlacementQuery
    {
        private readonly CampingConfig _config;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly Collider[] _overlaps = new Collider[16];

        public PhysicsCampPlacementQuery(CampingConfig config) => _config = config;

        public bool TryAim(Vector3 eye, Vector3 direction, float reach, Transform ignore, out CampGround ground)
        {
            ground = default;
            int count = Physics.RaycastNonAlloc(eye, direction.normalized, _hits, reach, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.distance >= nearest || hit.collider.transform.IsChildOf(ignore)) continue;
                nearest = hit.distance;
                ground = new CampGround(hit.point, hit.normal, hit.collider.GetComponentInParent<IAirshipView>() != null);
            }
            return !float.IsPositiveInfinity(nearest);
        }

        public bool HasRoom(Vector3 point, float radius, float height, Transform ignore)
        {
            float lift = CampPlacementProcessor.RoomLift(radius, _config.MaxSlopeDegrees);
            var bottom = point + Vector3.up * (lift + radius);
            var top = point + Vector3.up * Mathf.Max(lift + radius, height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!_overlaps[i].transform.IsChildOf(ignore)) return false;
            }
            return true;
        }
    }
}
