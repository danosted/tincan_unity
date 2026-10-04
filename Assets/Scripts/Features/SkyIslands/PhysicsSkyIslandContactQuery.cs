#nullable enable
using System.Collections.Generic;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Finds the ship in island rock with physics, only when an island stands within reach of it (the builder knows where
    /// they are, so a ship in open sky costs no query). An overlap box around the ship's solid colliders (bounds from the last
    /// physics sync, padded for this tick's movement) collects the island pieces near it (<see cref="SkyIslandBody"/>);
    /// each convex piece is then tested against each ship collider at their current poses
    /// (<c>Physics.ComputePenetration</c>: one side must be convex, the islands' pieces are). The push takes the ship out
    /// of the deepest overlap, then of the deepest left from there, a few times over. Triggers (interaction
    /// volumes) do not count; players are not parented to the ship, so they never do.
    /// </summary>
    public sealed class PhysicsSkyIslandContactQuery : ISkyIslandContactQuery
    {
        private const float Padding = 2f;

        /// <summary>Shallower than this is touching, not inside: a ship resting against rock is left alone.</summary>
        public const float MinDepth = 0.02f;

        /// <summary>Pushes summed per query at most; each takes the ship out of the deepest overlap left.</summary>
        private const int MaxPasses = 4;

        /// <summary>Islands further than this from the ship's pivot cannot touch it; no physics query is made then.</summary>
        private const float ShipReach = 100f;

        private readonly Collider[] _nearby = new Collider[128];
        private readonly List<Collider> _shipColliders = new();
        private readonly List<Collider> _solid = new();
        private readonly List<(Collider Piece, SkyIslandId Island)> _pieces = new();
        private readonly ISkyIslandBuilder _islands;

        public PhysicsSkyIslandContactQuery(ISkyIslandBuilder islands) => _islands = islands;

        public bool TryPushOut(IAirshipView ship, out Vector3 push, out SkyIslandId island)
        {
            push = Vector3.zero;
            island = default;
            var root = (ship as Component)?.transform ?? ship.Transform;
            if (root == null || !_islands.AnyNear(root.position, ShipReach)) return false;
            if (!SolidColliders(root, out var bounds)) return false;

            bounds.Expand(Padding * 2f);
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, _nearby, Quaternion.identity, ~0,
                QueryTriggerInteraction.Ignore);

            _pieces.Clear();
            for (int i = 0; i < count; i++)
            {
                if (_nearby[i].TryGetComponent<SkyIslandBody>(out var body)) _pieces.Add((_nearby[i], body.Id));
            }
            if (_pieces.Count == 0) return false;

            // Out of the deepest overlap, then again from there: a hull across several pieces comes clear in one tick.
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                if (!Deepest(push, out var step, out var id)) break;
                if (pass == 0) island = id;
                push += step;
            }
            return push.sqrMagnitude > 0f;
        }

        /// <summary>The deepest overlap of any piece with any ship collider, with the ship moved by <paramref name="offset"/>.</summary>
        private bool Deepest(Vector3 offset, out Vector3 step, out SkyIslandId island)
        {
            step = Vector3.zero;
            island = default;
            float deepest = MinDepth;
            foreach (var (piece, id) in _pieces)
            {
                var pieceTransform = piece.transform;
                var pieceBounds = piece.bounds;
                pieceBounds.Expand(Padding * 2f);
                foreach (var part in _solid)
                {
                    var partBounds = part.bounds;
                    partBounds.center += offset;
                    if (!pieceBounds.Intersects(partBounds)) continue;
                    var partTransform = part.transform;
                    if (!Physics.ComputePenetration(piece, pieceTransform.position, pieceTransform.rotation,
                            part, partTransform.position + offset, partTransform.rotation, out var direction, out var distance)) continue;
                    if (distance <= deepest) continue;

                    // The direction moves the piece out of the ship; the ship goes the other way.
                    deepest = distance;
                    step = -direction * distance;
                    island = id;
                }
            }
            return deepest > MinDepth;
        }

        private bool SolidColliders(Transform root, out Bounds bounds)
        {
            bounds = default;
            root.GetComponentsInChildren(_shipColliders);
            _solid.Clear();
            foreach (var collider in _shipColliders)
            {
                if (collider.isTrigger || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                if (_solid.Count == 0) bounds = collider.bounds;
                else bounds.Encapsulate(collider.bounds);
                _solid.Add(collider);
            }
            return _solid.Count > 0;
        }
    }
}
