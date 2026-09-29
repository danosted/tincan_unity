#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// Ship cannons: the cannon fixture(s) on the ship, firing and sweeping shots on the server, and drawing barrels,
    /// the aiming arc and balls on every peer. A player mans a cannon by interacting with it (a station, so the Stations
    /// installer must be in the profile too), aims with their look and fires with Primary; Interact again leaves.
    /// Add a fixture to add a cannon: placement is data. Plan: .docs/plans/cannon-and-hazards.md.
    /// </summary>
    [CreateAssetMenu(fileName = "CannonFeatureInstaller", menuName = "TinCan/Features/Cannon Feature Installer")]
    public class CannonFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>
    {
        [SerializeField] private CannonConfig? _config;
        [Tooltip("Cannon stations and where they sit on the ship (one fixture per cannon).")]
        [SerializeField] private List<ShipFixtureDefinition> _cannonFixtures = new();

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No CannonConfig assigned; cannons are off.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<CannonballProcessor>(Lifetime.Transient);
            builder.Register<CannonAimProcessor>(Lifetime.Transient);
            builder.Register<CannonFireUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
            builder.Register<CannonShotPresenter>(Lifetime.Singleton).AsSelf().As<ITickable>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                foreach (var fixture in _cannonFixtures)
                {
                    if (fixture != null && fixture.Prefab != null) yield return fixture.Prefab;
                }
            }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions => _cannonFixtures;
    }
}
