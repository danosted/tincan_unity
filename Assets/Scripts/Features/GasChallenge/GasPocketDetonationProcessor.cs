#nullable enable
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    public static class GasPocketDetonationProcessor
    {
        public static bool ShouldDetonate(GasPocketVolume? pocket, Collider? shipCollider)
        {
            if (pocket == null || shipCollider == null) return false;
            if (pocket.HasDetonated) return false;

            return pocket.ContainsCollider(shipCollider);
        }
    }
}
