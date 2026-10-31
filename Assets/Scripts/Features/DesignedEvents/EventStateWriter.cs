#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Hud;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Server: writes what the director and <see cref="Announce"/> do into the replicated <see cref="IEventState"/>, which
    /// <see cref="EventHudPresenter"/> shows on every peer. Without the EventState fixture (a ship without it, EditMode
    /// tests) announcements go straight to this peer's HUD, as in the POC.
    /// </summary>
    public sealed class EventStateWriter
    {
        private readonly IActorRegistry _actors;
        private readonly IHudValues _hud;

        public EventStateWriter(IActorRegistry actors, IHudValues hud)
        {
            _actors = actors;
            _hud = hud;
        }

        public void Running(int eventId, int phaseIndex) => State()?.ServerSetRunning(eventId, phaseIndex);

        public void Finished(int eventId, EventOutcome outcome) => State()?.ServerSetFinished(eventId, outcome);

        /// <summary>The event was dropped (session change): nothing runs and nothing is announced.</summary>
        public void Dropped()
        {
            var state = State();
            if (state == null)
            {
                _hud.Remove(EventHudPresenter.HudKey);
                return;
            }

            state.ServerSetRunning(0, 0);
            state.ServerSetAnnouncement(string.Empty);
        }

        public void Announce(string text)
        {
            var state = State();
            if (state != null) state.ServerSetAnnouncement(text);
            else _hud.Set(EventHudPresenter.HudKey, text);
        }

        private IEventState? State() => _actors.GetActors<IEventState>().FirstOrDefault(state => state.IsSimulating);
    }
}
