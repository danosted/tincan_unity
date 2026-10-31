#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Navigation;
using TinCan.Features.DesignedEvents;
using UnityEngine;
using VContainer;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// Gas pockets detonate when an airship flies into them (server, simulation tick), and the blast breaks the damage
    /// points near it, fittings first. Hand-placed pockets work on their own; the <see cref="GasChallengeDesignedEvents"/>
    /// GasSquall event lays a networked gas field ahead of the ship, which every peer sees and the chart shows
    /// (<see cref="GasPocketChartSource"/>, read only where Navigation is loaded). Builds on ship damage (the breaks and the
    /// repair phase) and designed events. Plan: .docs/plans/gas-squall-event.md.
    /// </summary>
    [CreateAssetMenu(fileName = "GasChallengeFeatureInstaller", menuName = "TinCan/Features/Gas Challenge Feature Installer")]
    public class GasChallengeFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<EventDefinition>
    {
        [SerializeField] private GasChallengeConfig? _config;

        public override void Install(IContainerBuilder builder)
        {
            var config = _config;
            if (config == null)
            {
                config = CreateInstance<GasChallengeConfig>();
                Debug.LogWarning($"[{name}] No GasChallengeConfig assigned; gas fields cannot spawn pockets.", this);
            }

            builder.RegisterInstance(config);
            builder.Register<PhysicsGasPocketQuery>(Lifetime.Singleton).As<IGasPocketQuery>();
            builder.Register<GasBlastProcessor>(Lifetime.Transient);
            builder.Register<GasChallengeUseCase>(Lifetime.Singleton).As<ISimulationTickable>();

            builder.Register<GasFieldProcessor>(Lifetime.Transient);
            builder.Register<GasPocketSpawningService>(Lifetime.Singleton).As<IGasPocketSpawner>();
            builder.Register<GasFieldUseCase>(Lifetime.Singleton).As<IGasField>().As<ISessionParticipant>();
            builder.Register<GasPocketChartSource>(Lifetime.Singleton).As<IChartSource>();

            // Designed-event vocabulary for gas fields; the event is GasChallengeDesignedEvents, contributed below.
            builder.Register<SpawnGasFieldActionHandler>(Lifetime.Singleton).As<IEventActionHandler>();
            builder.Register<ClearGasFieldActionHandler>(Lifetime.Singleton).As<IEventActionHandler>();
            builder.Register<GasFieldClearedConditionHandler>(Lifetime.Singleton).As<IEventConditionHandler>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_config != null && _config.PocketPrefab != null) yield return _config.PocketPrefab;
            }
        }

        IEnumerable<EventDefinition> FeatureInstaller.IExtension<EventDefinition>.Contributions => GasChallengeDesignedEvents.All;
    }
}
