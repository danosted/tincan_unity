#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.CloudMist
{
    /// <summary>
    /// The mist's quality tiers and which one draws (plan .docs/plans/cloud-sea-fog.md, F4). Shared by the installer
    /// (<see cref="CloudMistPresenter"/> picks the tier) and <see cref="CloudMistRendererFeature"/> on _MainURPRenderer,
    /// which draws nothing while <see cref="ActiveTier"/> is -1 (no installer, Off, or a level mapped to none).
    /// </summary>
    [CreateAssetMenu(fileName = "CloudMistQuality", menuName = "TinCan/Environment/Cloud Mist Quality")]
    public class CloudMistQuality : ScriptableObject
    {
        /// <summary>The player setting that picks the tier: Auto, Off, or a tier's name.</summary>
        public const string SettingKey = "graphics.clouds";

        [Tooltip("Cheapest first. The numbers are a sketch until measured (plan F4, step 3).")]
        [SerializeField] private CloudMistTier[] _tiers =
        {
            new("Low", 0.25f, 10, 0, 0, false, 250f),
            new("Medium", 0.25f, 16, 0, 1, true, 300f),
            new("High", 0.5f, 24, 0, 1, true, 400f),
            new("Ultra", 0.5f, 32, 3, 2, true, 500f),
        };

        [Tooltip("The tier (index into Tiers, -1 none) for each Unity quality level, Very Low first. Used when the setting is Auto.")]
        [SerializeField] private int[] _tierByQualityLevel = { -1, 0, 1, 2, 3, 3 };

        /// <summary>Runtime only, not saved: the tier drawing now, -1 none.</summary>
        [NonSerialized] public int ActiveTier = -1;

        public IReadOnlyList<CloudMistTier> Tiers => _tiers;
        public IReadOnlyList<int> TierByQualityLevel => _tierByQualityLevel;

        /// <summary>The tier drawing now, or null.</summary>
        public CloudMistTier? Active => ActiveTier >= 0 && ActiveTier < _tiers.Length ? _tiers[ActiveTier] : null;
    }
}
