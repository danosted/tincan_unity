#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Targeting;
using UnityEngine;

namespace TinCan.Core.Targeting
{
    /// <summary>What a query acquired, with the measurements that chose it.</summary>
    public readonly struct TargetResult
    {
        public readonly ITargetable Target;
        public readonly float Distance;
        public readonly float HorizontalAngle;

        public TargetResult(ITargetable target, float distance, float horizontalAngle)
        {
            Target = target;
            Distance = distance;
            HorizontalAngle = horizontalAngle;
        }
    }

    /// <summary>What ended a swept segment: a target (<see cref="Target"/> set) or a solid collider that is not one.</summary>
    public readonly struct SegmentHit
    {
        public readonly ITargetable? Target;
        public readonly Vector3 Point;
        public readonly float Distance;

        public SegmentHit(ITargetable? target, Vector3 point, float distance)
        {
            Target = target;
            Point = point;
            Distance = distance;
        }
    }

    /// <summary>
    /// The one place features and abilities ask "what is this actor aiming at?". Runs on any peer from simulated state;
    /// owners use it to predict (prompts, predicted abilities), the server's answer is authoritative.
    /// </summary>
    public interface ITargetingService
    {
        bool TryAcquire(ITargeter targeter, TargetingDefinition definition, out TargetResult result);

        /// <summary>
        /// As above, considering only targets <paramref name="accept"/> takes (Interact takes only interaction targets, so a
        /// sky hazard is never "what you can interact with"). The default filters the plain query's answer; the real
        /// service filters candidates before choosing.
        /// </summary>
        bool TryAcquire(ITargeter targeter, TargetingDefinition definition, Func<ITargetable, bool>? accept, out TargetResult result) =>
            TryAcquire(targeter, definition, out result) && (accept == null || accept(result.Target));

        /// <summary>
        /// Sweeps one straight piece of a path (a projectile's step this tick) with the definition's Radius and tag
        /// filters. The first accepted target or solid non-target collider ends it; triggers, filtered-out targets and
        /// colliders <paramref name="ignore"/> accepts are passed. The definition's aim source, shape and range are not
        /// used: the segment is the shape.
        /// </summary>
        bool TryAcquireSegment(Vector3 from, Vector3 to, TargetingDefinition definition, Func<Collider, bool>? ignore, out SegmentHit hit);
    }

    /// <summary>
    /// Application Layer: gathers candidates (the targetable registry for Cone and Sphere, physics for Ray), drops those
    /// that fail the definition's tag filters or line of sight, and lets <see cref="TargetingProcessor"/> pick.
    /// </summary>
    public class TargetingUseCase : ITargetingService
    {
        private const int MaxRayHits = 16;
        private const float DebugLineSeconds = 0.1f;

        private readonly ITargetableRegistry _targetables;
        private readonly TargetingProcessor _processor;
        private readonly List<ITargetable> _pool = new();
        private readonly List<TargetCandidate> _candidates = new();
        private readonly RaycastHit[] _hits = new RaycastHit[MaxRayHits];
        private readonly RaycastHit[] _sightHits = new RaycastHit[MaxRayHits];
        private readonly Dictionary<ITargetable, Collider[]> _colliders = new();

        public TargetingUseCase(ITargetableRegistry targetables, TargetingProcessor processor)
        {
            _targetables = targetables;
            _processor = processor;
        }

        public bool TryAcquire(ITargeter targeter, TargetingDefinition definition, out TargetResult result) =>
            TryAcquire(targeter, definition, null, out result);

        public bool TryAcquire(ITargeter targeter, TargetingDefinition definition, Func<ITargetable, bool>? accept, out TargetResult result)
        {
            result = default;
            if (!targeter.TryGetOrigin(out var origin)) return false;

            Vector3 source = _processor.SourcePoint(origin, definition);
            _pool.Clear();
            _candidates.Clear();

            switch (definition.Shape)
            {
                case TargetShape.Ray:
                    GatherRayHits(origin, source, definition, accept);
                    break;
                case TargetShape.Look:
                    if (!GatherLookHit(origin, source, definition, accept)) GatherClosestPoints(origin, source, definition, accept);
                    break;
                default:
                    GatherRegistered(source, definition, accept);
                    break;
            }

            bool acquired = _processor.TrySelect(origin, source, definition, _candidates, out var selection);
            if (acquired) result = new TargetResult(_pool[selection.Index], selection.Distance, selection.HorizontalAngle);

            // Queries often run on the network tick, not every frame; hold the lines long enough to bridge ticks.
            if (TargetingDebug.DrawAllQueries) TargetingGizmos.DrawDebug(origin, definition, acquired ? result.Target : null, DebugLineSeconds);
            return acquired;
        }

        public bool TryAcquireSegment(Vector3 from, Vector3 to, TargetingDefinition definition, Func<Collider, bool>? ignore, out SegmentHit hit)
        {
            hit = default;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length <= Mathf.Epsilon) return false;

            var ray = new Ray(from, delta / length);
            int count = definition.Radius > 0f
                ? Physics.SphereCastNonAlloc(ray, definition.Radius, _hits, length, ~0, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(ray, _hits, length, ~0, QueryTriggerInteraction.Collide);

            Array.Sort(_hits, 0, count, HitDistanceComparer.Instance);
            bool ended = false;
            for (int i = 0; i < count && !ended; i++)
            {
                var collider = _hits[i].collider;
                if (ignore != null && ignore(collider)) continue;

                // A cast that starts inside a collider reports distance 0 and no point; the segment's start stands in.
                Vector3 point = _hits[i].distance > 0f ? _hits[i].point : from;
                var targetable = collider.GetComponentInParent<ITargetable>();
                if (targetable == null)
                {
                    if (collider.isTrigger) continue;
                    hit = new SegmentHit(null, point, _hits[i].distance);
                    ended = true;
                }
                else if (Accepts(targetable, definition))
                {
                    hit = new SegmentHit(targetable, point, _hits[i].distance);
                    ended = true;
                }
            }

            if (TargetingDebug.DrawAllQueries) Debug.DrawLine(from, ended ? hit.Point : to, ended ? Color.red : Color.yellow, DebugLineSeconds);
            return ended;
        }

        private void GatherRegistered(Vector3 source, TargetingDefinition definition, Func<ITargetable, bool>? accept)
        {
            foreach (var targetable in _targetables.All)
            {
                if (!Accepts(targetable, definition, accept)) continue;
                Vector3 point = targetable.AimPoint;
                if (definition.RequireLineOfSight && IsBlocked(source, point, definition.BlockingMask)) continue;

                _candidates.Add(new TargetCandidate(_pool.Count, point));
                _pool.Add(targetable);
            }
        }

        private void GatherRayHits(in TargetingOrigin origin, Vector3 source, TargetingDefinition definition, Func<ITargetable, bool>? accept)
        {
            var ray = new Ray(source, _processor.Direction(origin, definition));
            int count = definition.Radius > 0f
                ? Physics.SphereCastNonAlloc(ray, definition.Radius, _hits, definition.Range, ~0, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(ray, _hits, definition.Range, ~0, QueryTriggerInteraction.Collide);

            // Walk the hits nearest first. A solid collider that is not a target stops the ray (a wall, another player),
            // as the old interaction ray did; triggers that are not targets (volumes the player stands in) are passed.
            // A ray finds targets by their colliders, so it does not need the registry (fixtures register late).
            Array.Sort(_hits, 0, count, HitDistanceComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                var targetable = _hits[i].collider.GetComponentInParent<ITargetable>();
                if (targetable == null)
                {
                    if (_hits[i].collider.isTrigger) continue;
                    break;
                }

                if (!Accepts(targetable, definition, accept)) continue;
                if (definition.RequireLineOfSight && IsBlocked(source, targetable.AimPoint, definition.BlockingMask)) continue;

                _candidates.Add(new TargetCandidate(_pool.Count, targetable.AimPoint, _hits[i].distance));
                _pool.Add(targetable);
            }
        }

        /// <summary>
        /// Look, first: the ray along the aim. The first accepted target it hits within range is the only candidate. A solid
        /// collider that is not an accepted target ends it (a wall, the deck, another player); triggers are passed, and so
        /// are colliders the cast starts inside (the targeter's own body), which report distance 0.
        /// </summary>
        private bool GatherLookHit(in TargetingOrigin origin, Vector3 source, TargetingDefinition definition, Func<ITargetable, bool>? accept)
        {
            var ray = new Ray(source, _processor.Direction(origin, definition));
            int count = definition.Radius > 0f
                ? Physics.SphereCastNonAlloc(ray, definition.Radius, _hits, definition.Range, ~0, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(ray, _hits, definition.Range, ~0, QueryTriggerInteraction.Collide);

            Array.Sort(_hits, 0, count, HitDistanceComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.distance <= 0f) continue;

                var targetable = hit.collider.GetComponentInParent<ITargetable>();
                if (targetable == null || !Accepts(targetable, definition, accept))
                {
                    if (hit.collider.isTrigger) continue;
                    return false;
                }

                _candidates.Add(new TargetCandidate(_pool.Count, hit.point, hit.distance));
                _pool.Add(targetable);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Look, when the ray found nothing: every accepted registered target, at the point of its colliders closest to the
        /// aim (its pivot when it has none), so a tall or wide object is reachable from any side. Line of sight, when
        /// required, ignores the target's own colliders.
        /// </summary>
        private void GatherClosestPoints(in TargetingOrigin origin, Vector3 source, TargetingDefinition definition, Func<ITargetable, bool>? accept)
        {
            Vector3 direction = _processor.Direction(origin, definition);
            foreach (var targetable in _targetables.All)
            {
                if (!Accepts(targetable, definition, accept)) continue;
                Vector3 point = ClosestPointToAim(targetable, source, direction, definition.Range);
                if (definition.RequireLineOfSight && IsBlocked(source, point, definition.BlockingMask, targetable)) continue;

                _candidates.Add(new TargetCandidate(_pool.Count, point));
                _pool.Add(targetable);
            }
        }

        private Vector3 ClosestPointToAim(ITargetable targetable, Vector3 source, Vector3 direction, float range)
        {
            var colliders = CollidersOf(targetable);
            Vector3 best = targetable.AimPoint;
            float bestAngle = float.PositiveInfinity;
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                Vector3 point = ClosestPointToRay(collider, source, direction, range);
                float angle = Vector3.Angle(direction, point - source);
                if (angle >= bestAngle) continue;
                bestAngle = angle;
                best = point;
            }
            return best;
        }

        /// <summary>
        /// The collider's point nearest the ray, near enough for ranking: the ray's point abreast of the collider's centre
        /// (clamped to the range), then the collider's point closest to that. Non-convex mesh colliders use their bounds.
        /// </summary>
        public static Vector3 ClosestPointToRay(Collider collider, Vector3 source, Vector3 direction, float range)
        {
            float along = Mathf.Clamp(Vector3.Dot(collider.bounds.center - source, direction), 0f, range);
            Vector3 probe = source + direction * along;
            bool exact = collider is BoxCollider or SphereCollider or CapsuleCollider || collider is MeshCollider { convex: true };
            return exact ? collider.ClosestPoint(probe) : collider.ClosestPointOnBounds(probe);
        }

        // A target's colliders, without those of a nested target (a fixture's own entity). Cached: hierarchies are fixed.
        private Collider[] CollidersOf(ITargetable targetable)
        {
            if (_colliders.TryGetValue(targetable, out var cached) && Array.TrueForAll(cached, collider => collider != null)) return cached;
            if (_colliders.Count > 512) _colliders.Clear(); // despawned targets leave stale keys; start over now and then

            var found = targetable is Component component && component != null
                ? Array.FindAll(component.GetComponentsInChildren<Collider>(true), collider => ReferenceEquals(collider.GetComponentInParent<ITargetable>(), targetable))
                : Array.Empty<Collider>();
            _colliders[targetable] = found;
            return found;
        }

        // Line of sight to a point on the target: anything solid in between blocks, except the target itself and colliders
        // the segment starts inside (the targeter's own body).
        private bool IsBlocked(Vector3 from, Vector3 to, LayerMask mask, ITargetable target)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude - 0.02f;
            if (length <= 0f) return false;

            int count = Physics.RaycastNonAlloc(new Ray(from, delta.normalized), _sightHits, length, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_sightHits[i].distance <= 0f) continue;
                if (ReferenceEquals(_sightHits[i].collider.GetComponentInParent<ITargetable>(), target)) continue;
                return true;
            }
            return false;
        }

        private sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        private static bool Accepts(ITargetable targetable, TargetingDefinition definition, Func<ITargetable, bool>? accept = null)
        {
            if (!targetable.IsTargetable || (accept != null && !accept(targetable))) return false;
            if (definition.RequiredTags.Count == 0 && definition.BlockedTags.Count == 0) return true;

            var controller = targetable.Controller;
            if (controller == null) return definition.RequiredTags.Count == 0;
            return HasAll(controller, definition.RequiredTags) && HasNone(controller, definition.BlockedTags);
        }

        private static bool HasAll(IAbilityControllerBase controller, List<GameplayTag> tags)
        {
            foreach (var tag in tags)
            {
                if (tag != null && !controller.HasTag(tag)) return false;
            }
            return true;
        }

        private static bool HasNone(IAbilityControllerBase controller, List<GameplayTag> tags)
        {
            foreach (var tag in tags)
            {
                if (tag != null && controller.HasTag(tag)) return false;
            }
            return true;
        }

        private static bool IsBlocked(Vector3 from, Vector3 to, LayerMask mask) =>
            Physics.Linecast(from, to, mask, QueryTriggerInteraction.Ignore);
    }
}
