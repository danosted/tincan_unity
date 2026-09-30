#nullable enable
using System.Collections.Generic;
using TinCan.Core.Items;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Interaction;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Fuel loop feature: drain while driven, stall when empty, jerry-can crate, motor refuel, HUD readout.
    /// The FuelSystem fixture (tank, crate, motor, rack, gauge) is spawned onto every airship at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "FuelFeatureInstaller", menuName = "TinCan/Features/Fuel Feature Installer")]
    public class FuelFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>,
        FeatureInstaller.IExtension<ItemDefinition>
    {
        [Tooltip("Networked FuelSystem prefab and where it sits on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _fuelSystemFixture;
        [Tooltip("The items this feature owns (the jerry can); they exist only in scenes that load fuel.")]
        [SerializeField] private List<ItemDefinition> _items = new();

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
                if (_fuelSystemFixture != null && _fuelSystemFixture.Prefab != null) yield return _fuelSystemFixture.Prefab;
            }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get
            {
                if (_fuelSystemFixture != null) yield return _fuelSystemFixture;
            }
        }

        IEnumerable<ItemDefinition> FeatureInstaller.IExtension<ItemDefinition>.Contributions => _items;
    }
}
