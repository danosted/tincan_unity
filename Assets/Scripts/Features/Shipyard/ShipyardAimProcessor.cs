#nullable enable
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Pure: turns the cursor's ray into the frame element a part would hang on (.docs/plans/frame-only-ships.md). A face
    /// part goes on the nearest plank floor the ray comes down onto, at the hit point snapped to the frame step (or the
    /// hit itself where the snapped point is off that floor). A node part goes on the node nearest the camera within the
    /// pick radius of the ray. Remove takes the part the ray hits.
    /// </summary>
    public sealed class ShipyardAimProcessor
    {
        /// <summary>How far behind the hit surface the part point is: one frame unit.</summary>
        private const float Inside = 0.1f;

        public ShipyardAim Aim(ShipyardRay ray, ShipFrameShape frame, ShipPartAttach attach, int step, float nodeRadius)
        {
            Vector3? partPoint = ray.HasHit ? ray.HitPoint - ray.HitNormal.normalized * Inside : null;
            var direction = ray.Direction.normalized;
            if (attach == ShipPartAttach.Node)
            {
                return TryNode(frame.Frame, ray.Origin, direction, nodeRadius, out int node)
                    ? new ShipyardAim(ShipPartAnchor.OnNode(node), partPoint)
                    : ShipyardAim.Nowhere(partPoint);
            }

            if (!TryFloor(frame, ray.Origin, direction, out int face, out var hit)) return ShipyardAim.Nowhere(partPoint);

            int size = Mathf.Max(1, step);
            var snapped = ShipPartAnchor.OnFace(face, Snap(hit.x, size), Snap(hit.z, size));
            var anchor = ShipPartPose.TryGetAnchorPoint(snapped, frame, out _)
                ? snapped
                : ShipPartAnchor.OnFace(face, ShipFrameUnits.FromMetres(hit.x), ShipFrameUnits.FromMetres(hit.z));
            return new ShipyardAim(anchor, partPoint);
        }

        private static int Snap(float metres, int step) => Mathf.RoundToInt(ShipFrameUnits.FromMetres(metres) / (float)step) * step;

        /// <summary>The nearest plank floor the ray comes down onto (from above), and where.</summary>
        private static bool TryFloor(ShipFrameShape frame, Vector3 origin, Vector3 direction, out int face, out Vector3 hit)
        {
            face = 0;
            hit = default;
            if (direction.y >= 0f) return false;

            float nearest = float.MaxValue;
            foreach (var floor in frame.PlankFloors)
            {
                foreach (var (a, b, c) in floor.EachTriangle())
                {
                    if (!ShipyardFramePickProcessor.Hits(origin, direction, a, b, c, out float t) || t >= nearest) continue;
                    face = floor.Face.Id;
                    nearest = t;
                }
            }

            if (face == 0) return false;
            hit = origin + direction * nearest;
            return true;
        }

        /// <summary>The node nearest the camera within the radius of the ray.</summary>
        private static bool TryNode(ShipFrame frame, Vector3 origin, Vector3 direction, float radius, out int node)
        {
            node = 0;
            float nearest = float.MaxValue;
            foreach (var candidate in frame.Nodes)
            {
                var p = frame.Position(candidate);
                float t = Vector3.Dot(p - origin, direction);
                if (t <= 0f || t >= nearest || Vector3.Distance(origin + direction * t, p) > radius) continue;
                node = candidate.Id;
                nearest = t;
            }

            return node != 0;
        }
    }
}
