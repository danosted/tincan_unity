#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Settings;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.CloudMist
{
    /// <summary>
    /// The mist near the camera, where the cloud sea turns from a drawn surface into a volume (plan
    /// .docs/plans/cloud-sea-fog.md, F4). The drawing is <see cref="CloudMistRendererFeature"/> on _MainURPRenderer,
    /// which reads the same <see cref="CloudMistQuality"/>; without this installer it draws nothing. Load it only where
    /// the Cloud Sea feature is, whose globals give the mist its shape. Contributes the graphics.clouds setting.
    /// Built by TinCan > Dev > Cloud Mist > Build Assets.
    /// </summary>
    [CreateAssetMenu(fileName = "CloudMistFeatureInstaller", menuName = "TinCan/Features/Cloud Mist Feature Installer")]
    public class CloudMistFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<SettingDefinition>
    {
        [Tooltip("Assets/Settings/CloudMist/CloudMistQuality (the renderer feature references the same asset).")]
        [SerializeField] private CloudMistQuality? _quality;
        [Tooltip("Assets/Settings/PlayerSettings/Setting_Clouds (graphics.clouds).")]
        [SerializeField] private SettingDefinition? _setting;

        public IEnumerable<SettingDefinition> Contributions
        {
            get { if (_setting != null) yield return _setting; }
        }

        public override void Install(IContainerBuilder builder)
        {
            if (_quality == null)
            {
                Debug.LogWarning($"[{name}] No CloudMistQuality assigned; there is no mist.", this);
                return;
            }

            builder.RegisterInstance(_quality);
            builder.Register<CloudMistTierProcessor>(Lifetime.Singleton);
            builder.Register<CloudMistShadowRange>(Lifetime.Singleton);
            builder.Register<CloudMistPresenter>(Lifetime.Singleton).As<ITickable>();
        }
    }
}
