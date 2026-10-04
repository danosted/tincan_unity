#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Targeting;
using UnityEngine;

namespace TinCan.Core.Targeting
{
    /// <summary>A candidate for selection: its index in the caller's list, where it is, and (Ray) how far along the ray it was hit.</summary>
    public readonly struct TargetCandidate
    {
        public readonly int Index;
        public readonly Vector3 Point;
        public readonly float? HitDistance;

        public TargetCandidate(int index, Vector3 point, float? hitDistance = null)
        {
            Index = index;
            Point = point;
            HitDistance = hitDistance;
        }
    }

    /// <summary>The selected candidate, with the measurements that chose it (useful to explain a miss or a pick).</summary>
    public readonly struct TargetSelectionResult
    {
        public readonly int Index;
        public readonly float Distance;
        public readonly float HorizontalAngle;

        public TargetSelectionResult(int index, float distance, float horizontalAngle)
        {
            Index = index;
            Distance = distance;
            HorizontalAngle = horizontalAngle;
        }
    }

    /// <summary>
    /// Domain Layer: the geometry of targeting. Where a definition aims from, whether a candidate is inside its shape,
    /// and which candidate wins. Pure: filtering by tags, line of sight and physics hits happen before this.
    /// </summary>
    public class TargetingProcessor
    {
        /// <summary>
        /// Where the query measures from: the source's base point (body, orbit centre or eye) moved by the definition's
        /// body-space offset, for example up and back so the cone also takes in what is right at the player's feet.
        /// </summary>
        public Vector3 SourcePoint(in TargetingOrigin origin, TargetingDefinition definition)
        {
            Vector3 basePoint = definition.Source switch
            {
                AimSource.BodyOffset => origin.BodyPosition,
                AimSource.CameraAim => origin.OrbitCentre,
                _ => origin.Eye
            };
            return basePoint + origin.BodyRotation * definition.SourceOffset;
        }

        /// <summary>The direction a ray follows: the pitched aim for Eye/CameraAim, the body's level facing otherwise.</summary>
        public Vector3 Direction(in TargetingOrigin origin, TargetingDefinition definition) =>
            UsesPitch(definition) ? origin.AimDirection : origin.Forward;

        /// <summary>The elevation a cone's vertical angle is centred on: the aim's for pitched sources, level otherwise.</summary>
        public float ReferenceElevation(in TargetingOrigin origin, TargetingDefinition definition) =>
            UsesPitch(definition) ? origin.AimElevation : 0f;

        private static bool UsesPitch(TargetingDefinition definition) =>
            definition.Source is AimSource.EyeAim or AimSource.CameraAim;

        /// <summary>Inside the definition's shape, measured from <paramref name="source"/> along the origin's facing.</summary>
        public bool IsInside(in TargetingOrigin origin, Vector3 source, TargetingDefinition definition, Vector3 point, float? hitDistance = null)
        {
            Vector3 offset = point - source;
            switch (definition.Shape)
            {
                case TargetShape.Sphere:
                    return offset.sqrMagnitude <= definition.Radius * definition.Radius;
                case TargetShape.Cone:
                    return InCone(origin, definition, offset);
                case TargetShape.Look:
                    // A look-ray hit is in reach by where the ray hit it; a fallback candidate must be in the cone.
                    return hitDistance is { } hit ? hit <= definition.Range : InCone(origin, definition, offset);
                default:
                    // Ray candidates come from physics hits along the ray; the range applies to where the ray hit them, not
                    // to their pivot (a tall trigger volume is hit well before its pivot on the deck).
                    return (hitDistance ?? offset.magnitude) <= definition.Range;
            }
        }

        public bool TrySelect(in TargetingOrigin origin, Vector3 source, TargetingDefinition definition,
            IReadOnlyList<TargetCandidate> candidates, out TargetSelectionResult result)
        {
            result = default;
            bool found = false;
            float bestPrimary = float.PositiveInfinity;
            float bestSecondary = float.PositiveInfinity;

            foreach (var candidate in candidates)
            {
                if (!IsInside(origin, source, definition, candidate.Point, candidate.HitDistance)) continue;

                Vector3 offset = candidate.Point - source;
                float distance = offset.magnitude;
                float horizontal = Angles(origin, offset).Horizontal;
                var (primary, secondary) = definition.Selection switch
                {
                    TargetSelection.BestAligned => (definition.Shape == TargetShape.Look ? AimAngle(origin, definition, offset) : horizontal, distance),
                    TargetSelection.FirstHit => (candidate.HitDistance ?? distance, distance),
                    _ => (distance, horizontal)
                };

                if (primary > bestPrimary || (Mathf.Approximately(primary, bestPrimary) && secondary >= bestSecondary)) continue;

                bestPrimary = primary;
                bestSecondary = secondary;
                result = new TargetSelectionResult(candidate.Index, distance, horizontal);
                found = true;
            }

            return found;
        }

        private bool InCone(in TargetingOrigin origin, TargetingDefinition definition, Vector3 offset)
        {
            if (offset.sqrMagnitude > definition.Range * definition.Range) return false;
            var (horizontal, vertical) = Angles(origin, offset);
            return horizontal <= definition.HorizontalAngle * 0.5f &&
                   Mathf.Abs(vertical - ReferenceElevation(origin, definition)) <= definition.VerticalAngle * 0.5f;
        }

        /// <summary>The full angle in degrees between the aim direction and the offset: how far off the look a point is.</summary>
        public float AimAngle(in TargetingOrigin origin, TargetingDefinition definition, Vector3 offset) =>
            offset.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(Direction(origin, definition), offset);

        /// <summary>
        /// Horizontal angle between the facing and the offset, and the offset's elevation above the body's horizontal
        /// plane, both in degrees. A point straight above or below counts as in front (horizontal 0).
        /// </summary>
        public static (float Horizontal, float Vertical) Angles(in TargetingOrigin origin, Vector3 offset)
        {
            Vector3 up = origin.Up;
            Vector3 flat = Vector3.ProjectOnPlane(offset, up);
            Vector3 forward = Vector3.ProjectOnPlane(origin.Forward, up);
            float horizontal = flat.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(forward, flat);
            float vertical = Mathf.Atan2(Vector3.Dot(offset, up), flat.magnitude) * Mathf.Rad2Deg;
            return (horizontal, vertical);
        }
    }
}
