#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Domain Layer: which broken part a repairing player works on. The nearest broken part within reach and inside a
    /// horizontal cone around where the player faces; height only counts towards distance, so looking up or down at a
    /// part at chest height does not matter.
    /// </summary>
    public class RepairTargetProcessor
    {
        /// <returns>The chosen part's index, or -1 when no broken part is in reach and in front.</returns>
        public int FindTarget(Vector3 origin, Vector3 forward, IReadOnlyList<(int Index, Vector3 Position, bool Broken)> parts, float reach, float coneDegrees)
        {
            Vector3 flatForward = new(forward.x, 0f, forward.z);
            flatForward = flatForward.sqrMagnitude > 0.0001f ? flatForward.normalized : Vector3.forward;
            float bestSqr = reach * reach;
            int best = -1;

            foreach (var part in parts)
            {
                if (!part.Broken) continue;

                Vector3 offset = part.Position - origin;
                float sqr = offset.sqrMagnitude;
                if (sqr > bestSqr) continue;

                Vector3 flatOffset = new(offset.x, 0f, offset.z);
                // Standing on top of a part counts as facing it.
                if (flatOffset.sqrMagnitude > 0.0001f && Vector3.Angle(flatForward, flatOffset) > coneDegrees * 0.5f) continue;

                bestSqr = sqr;
                best = part.Index;
            }

            return best;
        }
    }
}
