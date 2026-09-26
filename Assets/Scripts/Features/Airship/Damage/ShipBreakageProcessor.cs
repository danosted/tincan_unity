#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>Domain Layer: when the next break happens and which healthy part it hits. Deterministic for a given Random.</summary>
    public class ShipBreakageProcessor
    {
        public float NextInterval(Random random, float min, float max)
        {
            float low = Math.Min(min, max);
            float high = Math.Max(min, max);
            return low + (float)random.NextDouble() * (high - low);
        }

        /// <summary>True when another part may break: fewer than <paramref name="maxBroken"/> are broken and one is healthy.</summary>
        public bool CanBreak(IReadOnlyList<bool> broken, int maxBroken)
        {
            int count = 0;
            foreach (bool isBroken in broken) if (isBroken) count++;
            return count < maxBroken && count < broken.Count;
        }

        /// <summary>A uniformly chosen healthy index, or -1 when every part is broken.</summary>
        public int PickHealthy(Random random, IReadOnlyList<bool> broken)
        {
            var healthy = new List<int>();
            for (int i = 0; i < broken.Count; i++) if (!broken[i]) healthy.Add(i);
            return healthy.Count == 0 ? -1 : healthy[random.Next(healthy.Count)];
        }
    }
}
