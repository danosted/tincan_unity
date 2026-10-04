#nullable enable
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.TargetOutline
{
    /// <summary>
    /// Outlines what Interact would act on, for the local player only (presentation, never replicated). The drawing is the
    /// TargetOutlineRendererFeature on _MainURPRenderer, which reads the same config; without this installer nothing is
    /// marked and the feature draws nothing. Plan: .docs/plans/interaction-targeting.md.
    /// </summary>
    [CreateAssetMenu(fileName = "TargetOutlineFeatureInstaller", menuName = "TinCan/Features/Target Outline Feature Installer")]
    public class TargetOutlineFeatureInstaller : FeatureInstaller
    {
        [Tooltip("Assets/Settings/Interaction/TargetOutlineConfig (the renderer feature references the same asset).")]
        [SerializeField] private TargetOutlineConfig? _config;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No TargetOutlineConfig assigned; Interact targets are not outlined.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<TargetOutlinePresenter>(Lifetime.Singleton).AsSelf().As<ILateTickable>();
        }
    }
}
