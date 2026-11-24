#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Features.CloudMist
{
    /// <summary>
    /// Which mist tier draws: the player's choice (<see cref="CloudMistQuality.SettingKey"/>) by tier name, Off for none,
    /// and Auto (or anything unknown) by Unity's quality level through the asset's mapping. -1: no mist.
    /// </summary>
    public sealed class CloudMistTierProcessor
    {
        public const string Auto = "Auto";
        public const string Off = "Off";

        /// <param name="choice">The setting's value; null when there is no settings service (Auto).</param>
        /// <param name="qualityLevel">Unity's quality level now.</param>
        /// <param name="tierByLevel">The tier for each quality level, -1 for none. Levels past its end take its last entry.</param>
        public int Pick(string? choice, IReadOnlyList<CloudMistTier> tiers, int qualityLevel, IReadOnlyList<int> tierByLevel)
        {
            if (tiers.Count == 0) return -1;
            if (string.Equals(choice, Off, StringComparison.OrdinalIgnoreCase)) return -1;

            for (int i = 0; i < tiers.Count; i++)
            {
                if (string.Equals(choice, tiers[i].Name, StringComparison.OrdinalIgnoreCase)) return i;
            }

            if (tierByLevel.Count == 0) return -1;
            int tier = tierByLevel[Math.Clamp(qualityLevel, 0, tierByLevel.Count - 1)];
            return tier >= 0 && tier < tiers.Count ? tier : -1;
        }
    }
}
