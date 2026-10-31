#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Designed events: the <see cref="EventCatalog"/>, the server-side <see cref="EventDirectorUseCase"/> and the
    /// generic <see cref="Announce"/> action. Features contribute their events and their own action and condition
    /// handlers from their installers (ship damage: <c>HullStress</c>, <c>BreakShipPart</c>, <c>BrokenPartsAtMost</c>),
    /// so this feature references none of them. The EventState fixture on the ship replicates the running event and the
    /// announcement to every peer (<see cref="EventHudPresenter"/>).
    /// Needs the UI feature (HUD) for announcements.
    /// </summary>
    [CreateAssetMenu(fileName = "EventsFeatureInstaller", menuName = "TinCan/Features/Events Feature Installer")]
    public class EventsFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>
    {
        [SerializeField] private EventDirectorSettings _director = new();
        [Tooltip("Networked EventState prefab (NetworkObject, EntityNetworkMediator, EventStateNetworkMediator) on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _stateFixture;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_director);
            builder.Register<IReadOnlyList<EventDefinition>>(resolver => EventCatalog.Collect(resolver.Resolve<FeatureInstallerCatalog>()), Lifetime.Singleton);
            builder.Register<EventRunProcessor>(Lifetime.Transient);
            builder.Register<EventHandlerRegistry>(Lifetime.Singleton);
            builder.Register<EventStateWriter>(Lifetime.Singleton);
            builder.Register<EventDirectorUseCase>(Lifetime.Singleton).AsSelf().As<IEventDirector>().As<ISimulationTickable>().As<ISessionParticipant>();
            builder.Register<AnnounceActionHandler>(Lifetime.Singleton).As<IEventActionHandler>();
            builder.Register<EventHudPresenter>(Lifetime.Singleton).As<ITickable>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_stateFixture != null && _stateFixture.Prefab != null) yield return _stateFixture.Prefab;
            }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get
            {
                if (_stateFixture != null) yield return _stateFixture;
            }
        }
    }
}
