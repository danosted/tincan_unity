#nullable enable
using System;
using TinCan.Core.Domain.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer.Unity;

namespace TinCan.Features.CloudMist
{
    /// <summary>
    /// Picks the mist's tier each frame (<see cref="CloudMistTierProcessor"/>) from the player's graphics.clouds setting
    /// and Unity's quality level, for <see cref="CloudMistRendererFeature"/>, and stretches the sun's shadows to the
    /// tier's shadow range (<see cref="CloudMistShadowRange"/>) on the active URP asset, so the ship's and the islands'
    /// shadows reach the mist. The asset's own shadow settings come back when the tier has no range, and on Dispose: the
    /// change is runtime only (never saved), also in the Editor. Presentation only; on a server nothing renders.
    /// </summary>
    public sealed class CloudMistPresenter : ITickable, IDisposable
    {
        private readonly CloudMistQuality _quality;
        private readonly CloudMistTierProcessor _processor;
        private readonly CloudMistShadowRange _shadowRange;
        private readonly IPlayerSettings? _settings;

        // The asset being stretched and its own settings, to put back; null while nothing is stretched.
        private UniversalRenderPipelineAsset? _stretched;
        private float _ownDistance;
        private int _ownCascades;
        private Vector3 _ownSplits;
        private float _appliedRange;

        public CloudMistPresenter(CloudMistQuality quality, CloudMistTierProcessor processor, CloudMistShadowRange shadowRange,
            IPlayerSettings? settings)
        {
            _quality = quality;
            _processor = processor;
            _shadowRange = shadowRange;
            _settings = settings;
        }

        public void Tick()
        {
            string? choice = _settings != null && _settings.Has(CloudMistQuality.SettingKey)
                ? _settings.GetString(CloudMistQuality.SettingKey)
                : null;
            _quality.ActiveTier = _processor.Pick(choice, _quality.Tiers, QualitySettings.GetQualityLevel(), _quality.TierByQualityLevel);
            ApplyShadowRange(_quality.Active?.ShadowRange ?? 0f);
        }

        public void Dispose()
        {
            _quality.ActiveTier = -1;
            Restore();
        }

        private void ApplyShadowRange(float range)
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != _stretched) Restore();
            if (asset == null) return;
            if (_stretched != null && Mathf.Approximately(range, _appliedRange)) return;

            float ownDistance = _stretched != null ? _ownDistance : asset.shadowDistance;
            if (_shadowRange.Splits(ownDistance, range) is not { } splits)
            {
                Restore();
                return;
            }

            if (_stretched == null)
            {
                _stretched = asset;
                _ownDistance = asset.shadowDistance;
                _ownCascades = asset.shadowCascadeCount;
                _ownSplits = asset.cascade4Split;
            }

            asset.shadowDistance = range;
            asset.shadowCascadeCount = CloudMistShadowRange.Cascades;
            asset.cascade4Split = splits;
            _appliedRange = range;
        }

        private void Restore()
        {
            if (_stretched == null) return;
            _stretched.shadowDistance = _ownDistance;
            _stretched.shadowCascadeCount = _ownCascades;
            _stretched.cascade4Split = _ownSplits;
            _stretched = null;
            _appliedRange = 0f;
        }
    }
}
