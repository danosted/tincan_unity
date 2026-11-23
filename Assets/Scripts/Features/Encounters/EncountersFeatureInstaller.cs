#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Encounters
{
    /// <summary>
    /// Encounters: challenges are zones in the world, and outside them the world is quiet
    /// (<see cref="EncounterPressureUseCase"/>). E1: one zone from <see cref="_zone"/> (spawned by the server), its round
    /// (<see cref="EncounterZoneUseCase"/>), the zone query (<see cref="IEncounterZones"/>) and the HUD line. Loaded by the
    /// game profile (Profile_Galleon, cheesed_scene) in place of the voyage. Built by TinCan > Dev > Encounters > Build
    /// Assets. Plan: .docs/plans/encounters.md.
    /// </summary>
    [CreateAssetMenu(fileName = "EncountersFeatureInstaller", menuName = "TinCan/Features/Encounters Feature Installer")]
    public class EncountersFeatureInstaller : FeatureInstaller
    {
        [Tooltip("The first encounter zone (E1). Without one the world only stays quiet.")]
        [SerializeField] private EncounterZoneConfig? _zone;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<EncounterPressureUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
            builder.Register<EncounterZoneQuery>(Lifetime.Singleton).As<IEncounterZones>();
            builder.Register<EncounterHudPresenter>(Lifetime.Singleton).As<ITickable>();
            if (_zone == null) return;

            builder.RegisterInstance(_zone);
            builder.Register<NetworkEncounterZoneSpawner>(Lifetime.Singleton).As<IEncounterZoneSpawner>();
            builder.Register<EncounterZoneUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_zone != null && _zone.Prefab != null) yield return _zone.Prefab;
            }
        }
    }
}
