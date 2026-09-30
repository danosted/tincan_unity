#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Designed events: the <see cref="EventCatalog"/>, the server-side <see cref="EventDirectorUseCase"/> and the
    /// generic <see cref="Announce"/> action. Features contribute their events and their own action and condition
    /// handlers from their installers (ship damage: <c>HullStress</c>, <c>BreakShipPart</c>, <c>BrokenPartsAtMost</c>),
    /// so this feature references none of them.
    /// Needs the UI feature (HUD) for announcements.
    /// </summary>
    [CreateAssetMenu(fileName = "EventsFeatureInstaller", menuName = "TinCan/Features/Events Feature Installer")]
    public class EventsFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private EventDirectorSettings _director = new();

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_director);
            builder.Register<IReadOnlyList<EventDefinition>>(resolver => EventCatalog.Collect(resolver.Resolve<FeatureInstallerCatalog>()), Lifetime.Singleton);
            builder.Register<EventRunProcessor>(Lifetime.Transient);
            builder.Register<EventHandlerRegistry>(Lifetime.Singleton);
            builder.Register<EventDirectorUseCase>(Lifetime.Singleton).AsSelf().As<IEventDirector>().As<ISimulationTickable>().As<ISessionParticipant>();
            builder.Register<AnnounceActionHandler>(Lifetime.Singleton).As<IEventActionHandler>();
        }
    }
}
