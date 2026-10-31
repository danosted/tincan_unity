#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Hud;
using VContainer.Unity;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Presentation, every peer, every frame: the replicated event announcement as the HUD's "Event" line. Without the
    /// EventState fixture it leaves the line alone (<see cref="EventStateWriter"/> then writes it on the host).
    /// </summary>
    public sealed class EventHudPresenter : ITickable
    {
        public const string HudKey = "Event";

        private readonly IActorRegistry _actors;
        private readonly IHudValues _hud;
        private string? _shown;

        public EventHudPresenter(IActorRegistry actors, IHudValues hud)
        {
            _actors = actors;
            _hud = hud;
        }

        public void Tick()
        {
            var state = _actors.GetActors<IEventState>().FirstOrDefault(candidate => candidate.IsSimulating);
            if (state == null) return;

            string text = state.Announcement;
            if (text == _shown) return;

            _shown = text;
            if (text.Length == 0) _hud.Remove(HudKey);
            else _hud.Set(HudKey, text);
        }
    }
}
