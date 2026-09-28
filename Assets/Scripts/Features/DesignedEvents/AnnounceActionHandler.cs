#nullable enable
using TinCan.Features.UI;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Shows an <see cref="Announce"/> on the HUD. POC: the HUD is local, so only the host sees it; replicating the
    /// event state to clients is the next step (designed-events.md, "Replicated state").
    /// </summary>
    public sealed class AnnounceActionHandler : EventActionHandler<Announce>
    {
        public const string HudKey = "Event";

        private readonly IHudValues _hud;

        public AnnounceActionHandler(IHudValues hud) => _hud = hud;

        protected override bool Execute(Announce action)
        {
            _hud.Set(HudKey, action.Text);
            return true;
        }
    }
}
