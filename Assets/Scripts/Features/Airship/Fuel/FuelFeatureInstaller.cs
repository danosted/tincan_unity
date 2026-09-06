#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Features.Interaction;
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
    public class FuelFeatureInstaller : FeatureInstaller
    {
        [Tooltip("Networked FuelSystem prefab and where it sits on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _fuelSystemFixture;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<FuelConsumptionProcessor>(Lifetime.Transient);
            builder.Register<FuelConsumptionUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
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

        public override IEnumerable<ShipFixtureDefinition> ShipFixtures
        {
            get
            {
                if (_fuelSystemFixture != null) yield return _fuelSystemFixture;
            }
        }
    }
}
