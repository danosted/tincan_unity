#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Items;
using TinCan.Core.Gas.Cues;
using TinCan.Features.DesignedEvents;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Ship damage: breakable spots on the deck (the ShipDamageSockets fixture), random breakage, the hull-breach
    /// effect on the ship (fuel leak) and a HUD line. Switch it on or off by listing it in a scene's feature profile.
    /// Players repair with the repair tool from the tool rack fixture: holding it grants GA_RepairShip, and
    /// <see cref="ShipRepairUseCase"/> applies the repair effect to the broken part they face. Presentation comes as
    /// gameplay cues: the notifies listed here, and the marker's ToggleObjectCueHandler in the sockets prefab (both need
    /// GameplayCuesFeatureInstaller).
    /// </summary>
    [CreateAssetMenu(fileName = "ShipDamageFeatureInstaller", menuName = "TinCan/Features/Ship Damage Feature Installer")]
    public class ShipDamageFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>, FeatureInstaller.IExtension<GameplayCueNotify>,
        FeatureInstaller.IExtension<EventDefinition>, FeatureInstaller.IExtension<ItemDefinition>
    {
        [SerializeField] private ShipDamageConfig? _config;
        [Tooltip("Networked ShipDamageSockets prefab and where it sits on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _socketsFixture;
        [Tooltip("Networked rack that hands out the repair tool, and where it sits on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _toolRackFixture;
        [Tooltip("What break, repair, the leak and the repair tool look and sound like (GCN_* assets).")]
        [SerializeField] private List<GameplayCueNotify> _cueNotifies = new();
        [Tooltip("The items this feature owns (the repair tool).")]
        [SerializeField] private List<ItemDefinition> _items = new();

        public override void Install(IContainerBuilder builder)
        {
            var config = _config;
            if (config == null)
            {
                config = CreateInstance<ShipDamageConfig>();
                config.AutoBreak = false;
                Debug.LogWarning($"[{name}] No ShipDamageConfig assigned; random breakage disabled.", this);
            }

            builder.RegisterInstance(config);
            builder.Register<ShipBreakageProcessor>(Lifetime.Transient);
            builder.Register<ShipBreakageUseCase>(Lifetime.Singleton).AsSelf().As<IShipBreakage>().As<ISimulationTickable>();
            builder.Register<ShipRepairUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
            builder.Register<ShipDamageHudPresenter>(Lifetime.Singleton).As<ITickable>();

            // Designed-event vocabulary for ship damage; its events are ShipDamageDesignedEvents, contributed below.
            builder.Register<BreakShipPartActionHandler>(Lifetime.Singleton).As<IEventActionHandler>();
            builder.Register<BrokenPartsAtMostConditionHandler>(Lifetime.Singleton).As<IEventConditionHandler>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_socketsFixture != null && _socketsFixture.Prefab != null) yield return _socketsFixture.Prefab;
                if (_toolRackFixture != null && _toolRackFixture.Prefab != null) yield return _toolRackFixture.Prefab;
            }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get
            {
                if (_socketsFixture != null) yield return _socketsFixture;
                if (_toolRackFixture != null) yield return _toolRackFixture;
            }
        }

        IEnumerable<GameplayCueNotify> FeatureInstaller.IExtension<GameplayCueNotify>.Contributions => _cueNotifies;

        IEnumerable<EventDefinition> FeatureInstaller.IExtension<EventDefinition>.Contributions => ShipDamageDesignedEvents.All;

        IEnumerable<ItemDefinition> FeatureInstaller.IExtension<ItemDefinition>.Contributions => _items;
    }
}
