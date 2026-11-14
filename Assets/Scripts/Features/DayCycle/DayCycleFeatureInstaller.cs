#nullable enable
using TinCan.Core.Domain.Atmosphere;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Features.DayCycle
{
    /// <summary>
    /// The moving day: registers the <see cref="ISunSource"/> the Atmosphere feature lights the look by. Presentation
    /// only, the same on every peer from synced time. Built by TinCan > Dev > Look > Build Atmosphere, which puts it in
    /// the play profiles and the look's test area. Plan: .docs/plans/day-cycle.md (D2).
    /// </summary>
    [CreateAssetMenu(fileName = "DayCycleFeatureInstaller", menuName = "TinCan/Features/Day Cycle Feature Installer")]
    public class DayCycleFeatureInstaller : FeatureInstaller
    {
        [Tooltip("Assets/Settings/DayCycle/DayCycleConfig: the driver and the sun's path.")]
        [SerializeField] private DayCycleConfig? _config;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No DayCycleConfig assigned; the look keeps its fixed sun.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<SunPathProcessor>(Lifetime.Singleton);
            builder.Register<DayCycleUseCase>(Lifetime.Singleton).AsSelf().As<ISunSource>();
        }
    }
}
