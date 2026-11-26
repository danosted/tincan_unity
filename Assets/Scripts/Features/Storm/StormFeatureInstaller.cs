#nullable enable
using TinCan.Core.Domain.Features;
using TinCan.Features.Atmosphere;
using TinCan.Features.CloudBoundary.Atmosphere;
using TinCan.Features.Weapons.Cannon;
using TinCan.Core.Domain;
using TinCan.Core.Ship;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// The storm (plan: .docs/plans/storm-crossing.md): its light and air as the atmosphere's weather and the volumetric
    /// clouds', its region for the shaders (it towers and darkens where it stands, seen from afar), its lightning and its
    /// sound, its heart and the cure (the beam hits its targets, <see cref="StormCureUseCase"/>), and how it fights back:
    /// gusts (<see cref="StormGustUseCase"/>) and debris on the wind (<see cref="StormDebrisUseCase"/>), all driven by the
    /// nearest encounter zone (<see cref="StormIntensity"/>), or by the lab's controls in Lab_Storm. Needs the Atmosphere
    /// feature, Encounters with a zone, the cannons (the cure) and SkyHazards (the debris; without it, none comes); in
    /// the game's profile. Built by TinCan > Dev > Storm >
    /// Build Lab; the heart's parts by TinCan > Dev > Storm > Build Heart.
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
            // The heart and its cure (ST5): the server answers the beam; every peer draws the heart and its target.
            builder.Register<StormCureUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>().As<ISpiritBeamTargets>();
            builder.Register<StormHeartPresenter>(Lifetime.Singleton).AsSelf().As<ITickable>().As<System.IDisposable>();
            // The storm fights back (ST3, ST4), server: gusts shove the ship, and debris (sky hazards) rides the wind at it.
            builder.Register<StormGustUseCase>(Lifetime.Singleton).AsSelf().As<IAirshipFlightForces>();
            builder.Register<StormDebrisUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
        }
    }
}
