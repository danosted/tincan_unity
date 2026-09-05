#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>Domain Layer: where the net head is and which can (if any) it catches.</summary>
    public class CatchProcessor
    {
        public Vector3 NetPosition(Vector3 playerPosition, Vector3 playerForward, float reach, float height)
        {
            Vector3 flatForward = new(playerForward.x, 0f, playerForward.z);
            flatForward = flatForward.sqrMagnitude > 0.0001f ? flatForward.normalized : Vector3.forward;
            return playerPosition + flatForward * reach + Vector3.up * height;
        }

        public bool TryFindCatchable(Vector3 netPosition, float radius, IEnumerable<IFlyingCanView> cans, out IFlyingCanView? nearest)
        {
            nearest = null;
            float bestSqr = radius * radius;

            foreach (var can in cans)
            {
                var transform = can.Transform;
                if (transform == null) continue;

                float sqr = (transform.position - netPosition).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                nearest = can;
            }

            return nearest != null;
        }
    }
}
