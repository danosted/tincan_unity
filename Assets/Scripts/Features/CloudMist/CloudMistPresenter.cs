#nullable enable
using System;
using TinCan.Core.Domain.Settings;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.CloudMist
{
    /// <summary>
    /// Picks the mist's tier each frame (<see cref="CloudMistTierProcessor"/>) from the player's graphics.clouds setting
    /// and Unity's quality level, for <see cref="CloudMistRendererFeature"/>. Presentation only; on a server nothing
    /// renders, so the tier is never used there.
    /// </summary>
    public sealed class CloudMistPresenter : ITickable, IDisposable
    {
        private readonly CloudMistQuality _quality;
        private readonly CloudMistTierProcessor _processor;
        private readonly IPlayerSettings? _settings;

        public CloudMistPresenter(CloudMistQuality quality, CloudMistTierProcessor processor, IPlayerSettings? settings)
        {
            _quality = quality;
            _processor = processor;
            _settings = settings;
        }

        public void Tick()
        {
            string? choice = _settings != null && _settings.Has(CloudMistQuality.SettingKey)
                ? _settings.GetString(CloudMistQuality.SettingKey)
                : null;
            _quality.ActiveTier = _processor.Pick(choice, _quality.Tiers, QualitySettings.GetQualityLevel(), _quality.TierByQualityLevel);
        }

        public void Dispose() => _quality.ActiveTier = -1;
    }
}
