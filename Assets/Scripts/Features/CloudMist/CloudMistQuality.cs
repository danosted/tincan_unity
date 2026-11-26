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
            new("Low", 0.25f, 16f, 12, 0, 0, false),
            new("Medium", 0.25f, 10f, 24, 0, 1, true),
            new("High", 0.5f, 6f, 40, 0, 1, true, 400f),
            new("Ultra", 0.5f, 4f, 64, 0, 2, true, 600f),
        };

        [Tooltip("How far the mist reaches, as a share of the view distance (the camera's far plane). It fades out over " +
                 "its last part and the drawn sea carries the rest, where mist would read as flat haze anyway.")]
        [Range(0.1f, 1f)] [SerializeField] private float _reachShare = 0.6f;

        [Tooltip("The tier (index into Tiers, -1 none) for each Unity quality level, Very Low first. Used when the setting is Auto.")]
        [SerializeField] private int[] _tierByQualityLevel = { -1, 0, 1, 2, 3, 3 };

        /// <summary>Runtime only, not saved: the tier drawing now, -1 none.</summary>
        [NonSerialized] public int ActiveTier = -1;

        public IReadOnlyList<CloudMistTier> Tiers => _tiers;
        public IReadOnlyList<int> TierByQualityLevel => _tierByQualityLevel;

        /// <summary>How far the mist reaches, in metres, for a camera whose far plane is <paramref name="farClipPlane"/>.</summary>
        public float Reach(float farClipPlane) => farClipPlane * _reachShare;

        /// <summary>The tier drawing now, or null.</summary>
        public CloudMistTier? Active => ActiveTier >= 0 && ActiveTier < _tiers.Length ? _tiers[ActiveTier] : null;
    }
}
