#nullable enable
using System.Collections.Generic;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// Finds pockets by overlapping the ship's collider bounds with trigger colliders, then keeps the ones
    /// <see cref="GasPocketDetonationProcessor"/> says the ship really touches.
    /// </summary>
    public sealed class PhysicsGasPocketQuery : IGasPocketQuery
    {
        private readonly Collider[] _hits = new Collider[32];

        public void Touching(IAirshipView ship, List<GasPocketVolume> results)
        {
            results.Clear();
            var shipCollider = (ship as Component)?.GetComponentInChildren<Collider>();
            if (shipCollider == null) return;

            var bounds = shipCollider.bounds;
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, _hits, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var pocket = _hits[i].GetComponentInParent<GasPocketVolume>();
                if (pocket == null || results.Contains(pocket)) continue;
                if (GasPocketDetonationProcessor.ShouldDetonate(pocket, shipCollider)) results.Add(pocket);
            }
        }
    }
}
