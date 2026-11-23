#nullable enable
using TinCan.Core.Domain.Features;
using TinCan.Features.Atmosphere;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// The storm (plan: .docs/plans/storm-crossing.md). ST1, the lab: the storm's light and air as the atmosphere's
    /// weather, its lightning and its sound, driven by two sliders (<see cref="StormLabSettings"/>) in Lab_Storm. Without
    /// lab settings nothing drives it yet (the zone does from ST2), so it stays calm. Needs the Atmosphere feature.
    /// Built by TinCan > Dev > Storm > Build Lab.
    /// </summary>
    [CreateAssetMenu(fileName = "StormFeatureInstaller", menuName = "TinCan/Features/Storm Feature Installer")]
    public sealed class StormFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private StormConfig? _config;
        [Tooltip("The lab's sliders. Set only on the lab's profile.")]
        [SerializeField] private StormLabSettings? _lab;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null || _lab == null)
            {
                if (_config == null) Debug.LogWarning($"[{name}] No StormConfig assigned; the storm is off.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.RegisterInstance<IStormIntensity>(_lab);
            builder.Register<StormSoundPresenter>(Lifetime.Singleton);
            builder.Register<StormWeatherPresenter>(Lifetime.Singleton).AsSelf().As<IAtmosphereWeather>().As<ITickable>();
        }
    }
}
