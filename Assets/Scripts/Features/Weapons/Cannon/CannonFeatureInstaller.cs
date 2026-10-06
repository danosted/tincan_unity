#nullable enable
using TinCan.Core.Ship.Sockets;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;
using TinCan.Core.Humanoid;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// Ship cannons: the cannon fixture(s) on the ship, firing and sweeping shots on the server, and drawing barrels,
    /// the aiming arc and balls on every peer. A player mans a cannon by interacting with it (a station, so the Stations
    /// installer must be in the profile too). While manning it the Gunner input context is live: the mouse swings the
    /// barrel (<see cref="GunnerAimUseCase"/>) while the body holds still, Fire shoots and Leave steps away.
    /// Add a fixture to add a cannon: placement is data. Plan: .docs/plans/cannon-and-hazards.md.
    /// </summary>
    [CreateAssetMenu(fileName = "CannonFeatureInstaller", menuName = "TinCan/Features/Cannon Feature Installer")]
    public class CannonFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>, FeatureInstaller.IExtension<InputContext>,
        FeatureInstaller.IExtension<ShipFittingDefinition>
    {
        [SerializeField] private CannonConfig? _config;
        [Tooltip("Cannon stations and where they sit on the ship (one fixture per cannon).")]
        [SerializeField] private List<ShipFixtureDefinition> _cannonFixtures = new();
        [Tooltip("The cannon station as a fitting players mount in a ship's socket (ships built from designs).")]
        [SerializeField] private List<ShipFittingDefinition> _fittings = new();
        [Tooltip("Assets/Input/Contexts/Context_Gunner: the mouse aims the barrel while manning a cannon.")]
        [SerializeField] private GunnerInputContext? _controls;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null || _controls == null)
            {
                Debug.LogWarning($"[{name}] No CannonConfig or GunnerInputContext assigned; cannons are off.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.RegisterInstance(_controls);
            builder.Register<CannonballProcessor>(Lifetime.Transient);
            builder.Register<CannonAimProcessor>(Lifetime.Transient);
            builder.Register<GunnerAimUseCase>(Lifetime.Singleton).AsSelf().As<ITickable>().As<IHumanoidInputContributor>();
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

        IEnumerable<ShipFittingDefinition> FeatureInstaller.IExtension<ShipFittingDefinition>.Contributions => _fittings;

        IEnumerable<InputContext> FeatureInstaller.IExtension<InputContext>.Contributions
        {
            get { if (_controls != null) yield return _controls; }
        }
    }
}
