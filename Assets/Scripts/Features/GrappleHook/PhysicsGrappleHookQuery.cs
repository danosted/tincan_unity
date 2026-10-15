#nullable enable
using UnityEngine;

namespace TinCan.Features.GrappleHook
{
    /// <summary>The grappling hook's ray, through the physics scene: any solid collider catches it (ship, rock, deck props).</summary>
    public sealed class PhysicsGrappleHookQuery : IGrappleHookQuery
    {
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        public bool TryCatch(Vector3 from, Vector3 direction, float range, LayerMask layers, Transform holder,
            out Transform frame, out Vector3 point)
        {
            frame = null!;
            point = default;

            int count = Physics.RaycastNonAlloc(from, direction, _hits, range, layers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.distance >= nearest || hit.collider.transform.IsChildOf(holder)) continue;

                nearest = hit.distance;
                frame = hit.collider.transform;
                point = hit.point;
            }

            return frame != null;
        }
    }
}
