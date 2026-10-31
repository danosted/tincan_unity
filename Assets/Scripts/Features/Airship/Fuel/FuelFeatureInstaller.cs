#nullable enable
using System.Collections.Generic;
using TinCan.Core.Items;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Interaction;
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
        [Tooltip("Socket fittings: the receiver and the crystal rack.")]
        [SerializeField] private List<ShipFittingDefinition> _fittings = new();

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<FuelConsumptionProcessor>(Lifetime.Transient);
            builder.Register<FuelConsumptionUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>().As<ISessionParticipant>();
            builder.Register<PourFuelInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<TakeJerryCanInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<FuelHudPresenter>(Lifetime.Singleton).As<ITickable>();
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
