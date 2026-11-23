#nullable enable
using TinCan.Core.Domain.Features;
using TinCan.Features.Atmosphere;
using TinCan.Features.CloudBoundary.Atmosphere;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// The storm (plan: .docs/plans/storm-crossing.md): its light and air as the atmosphere's weather and the volumetric
    /// clouds', its region for the shaders (it towers and darkens where it stands, seen from afar), its lightning and its
    /// sound, all driven by the nearest encounter zone (<see cref="StormIntensity"/>), or by the lab's controls in Lab_Storm.
    /// Needs the Atmosphere feature; in the game's profile with Encounters. Built by TinCan > Dev > Storm > Build Lab.
    /// </summary>
    [CreateAssetMenu(fileName = "StormFeatureInstaller", menuName = "TinCan/Features/Storm Feature Installer")]
    public sealed class StormFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private StormConfig? _config;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No StormConfig assigned; the storm is off.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<StormIntensity>(Lifetime.Singleton).AsSelf().As<IStormIntensity>().As<ITickable>();
            builder.Register<StormSoundPresenter>(Lifetime.Singleton);
            builder.Register<StormWeatherPresenter>(Lifetime.Singleton).AsSelf().As<IAtmosphereWeather>().As<ICloudWeather>().As<ITickable>()
                .As<System.IDisposable>();
        }
    }
}
