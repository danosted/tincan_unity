#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Targeting;
using UnityEngine;

namespace TinCan.Features.Targeting
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

    /// <summary>
    /// The one place features and abilities ask "what is this actor aiming at?". Runs on any peer from simulated state;
    /// owners use it to predict (prompts, predicted abilities), the server's answer is authoritative.
    /// </summary>
    public interface ITargetingService
    {
        bool TryAcquire(ITargeter targeter, TargetingDefinition definition, out TargetResult result);
    }

    /// <summary>
    /// Application Layer: gathers candidates (the targetable registry for Cone and Sphere, physics for Ray), drops those
    /// that fail the definition's tag filters or line of sight, and lets <see cref="TargetingProcessor"/> pick.
    /// </summary>
    public class TargetingUseCase : ITargetingService
    {
        private const int MaxRayHits = 16;

        private readonly ITargetableRegistry _targetables;
        private readonly TargetingProcessor _processor;
        private readonly List<ITargetable> _pool = new();
        private readonly List<TargetCandidate> _candidates = new();
        private readonly RaycastHit[] _hits = new RaycastHit[MaxRayHits];

        public TargetingUseCase(ITargetableRegistry targetables, TargetingProcessor processor)
        {
            _targetables = targetables;
            _processor = processor;
        }

        public bool TryAcquire(ITargeter targeter, TargetingDefinition definition, out TargetResult result)
        {
            result = default;
            if (!targeter.TryGetOrigin(out var origin)) return false;

            Vector3 source = _processor.SourcePoint(origin, definition);
            _pool.Clear();
            _candidates.Clear();

            if (definition.Shape == TargetShape.Ray) GatherRayHits(origin, source, definition);
            else GatherRegistered(source, definition);

            if (!_processor.TrySelect(origin, source, definition, _candidates, out var selection)) return false;

            result = new TargetResult(_pool[selection.Index], selection.Distance, selection.HorizontalAngle);
            return true;
        }

        private void GatherRegistered(Vector3 source, TargetingDefinition definition)
        {
            foreach (var targetable in _targetables.All)
            {
                if (!Accepts(targetable, definition)) continue;
                Vector3 point = targetable.AimPoint;
                if (definition.RequireLineOfSight && IsBlocked(source, point, definition.BlockingMask)) continue;

                _candidates.Add(new TargetCandidate(_pool.Count, point));
                _pool.Add(targetable);
            }
        }

        private void GatherRayHits(in TargetingOrigin origin, Vector3 source, TargetingDefinition definition)
        {
            var ray = new Ray(source, _processor.Direction(origin, definition));
            int count = definition.Radius > 0f
                ? Physics.SphereCastNonAlloc(ray, definition.Radius, _hits, definition.Range, ~0, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(ray, _hits, definition.Range, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var targetable = _hits[i].collider.GetComponentInParent<ITargetable>();
                if (targetable == null || !Registered(targetable) || !Accepts(targetable, definition)) continue;
                if (definition.RequireLineOfSight && IsBlocked(source, targetable.AimPoint, definition.BlockingMask)) continue;

                _candidates.Add(new TargetCandidate(_pool.Count, targetable.AimPoint, _hits[i].distance));
                _pool.Add(targetable);
            }
        }

        private bool Registered(ITargetable targetable)
        {
            foreach (var known in _targetables.All)
            {
                if (ReferenceEquals(known, targetable)) return true;
            }
            return false;
        }

        private static bool Accepts(ITargetable targetable, TargetingDefinition definition)
        {
            if (!targetable.IsTargetable) return false;
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
