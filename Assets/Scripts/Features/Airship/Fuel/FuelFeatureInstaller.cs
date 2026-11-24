#nullable enable
using System.Collections.Generic;
using TinCan.Core.Items;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Features;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Sockets;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Fuel loop feature: drain while driven, stall when empty, jerry-can crate, motor refuel, HUD readout.
    /// The tank is ship state: the pose-less FuelTankState fixture is spawned onto every airship, so every ship drains
    /// and stalls. The old hull also gets the FuelSystem fixture (receiver, crystal rack, gauge); designed ships mount
    /// the receiver and the crystal rack as socket fittings (fuel.receiver, fuel.crystal_rack).
    /// </summary>
    [CreateAssetMenu(fileName = "FuelFeatureInstaller", menuName = "TinCan/Features/Fuel Feature Installer")]
    public class FuelFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>,
        FeatureInstaller.IExtension<ItemDefinition>, FeatureInstaller.IExtension<ShipFittingDefinition>
    {
        [Tooltip("The ship's fuel tank: a pose-less fixture on every airship.")]
        [SerializeField] private ShipFixtureDefinition? _tankStateFixture;
        [Tooltip("Networked FuelSystem prefab (receiver, crystal rack, gauge) and where it sits on the old hull.")]
        [SerializeField] private ShipFixtureDefinition? _fuelSystemFixture;
        [Tooltip("The items this feature owns (the jerry can); they exist only in scenes that load fuel.")]
        [SerializeField] private List<ItemDefinition> _items = new();
        [Tooltip("Socket fittings: the receiver, the crystal rack and the hand crank.")]
        [SerializeField] private List<ShipFittingDefinition> _fittings = new();
        [Tooltip("The input that turns a hand crank in reach (Input_Primary): one press, one turn. Unset: cranks do nothing.")]
        [SerializeField] private GameplayInput? _crankInput;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<FuelConsumptionProcessor>(Lifetime.Transient);
            builder.Register<FuelConsumptionUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>().As<ISessionParticipant>()
                .As<IShipRecovery>();
            builder.Register<FuelHudPresenter>(Lifetime.Singleton).As<ITickable>();
            builder.Register<CrankTurnUseCase>(Lifetime.Singleton).AsSelf().As<IHumanoidMotionContributor>().WithParameter(new CrankInput(_crankInput));
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_tankStateFixture != null && _tankStateFixture.Prefab != null) yield return _tankStateFixture.Prefab;
                if (_fuelSystemFixture != null && _fuelSystemFixture.Prefab != null) yield return _fuelSystemFixture.Prefab;
                foreach (var fitting in _fittings)
                {
                    if (fitting != null && fitting.Prefab != null) yield return fitting.Prefab;
                }
            }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get
            {
                if (_tankStateFixture != null) yield return _tankStateFixture;
                if (_fuelSystemFixture != null) yield return _fuelSystemFixture;
            }
        }

        IEnumerable<ItemDefinition> FeatureInstaller.IExtension<ItemDefinition>.Contributions => _items;

        IEnumerable<ShipFittingDefinition> FeatureInstaller.IExtension<ShipFittingDefinition>.Contributions => _fittings;
    }
}
