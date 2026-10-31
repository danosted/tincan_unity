#nullable enable
namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Runs an <see cref="Announce"/>: the line goes into the replicated event state, which every peer's
    /// <see cref="EventHudPresenter"/> shows (<see cref="EventStateWriter"/>).
    /// </summary>
    public sealed class AnnounceActionHandler : EventActionHandler<Announce>
    {
        public const string HudKey = EventHudPresenter.HudKey;

        private readonly EventStateWriter _state;

        public AnnounceActionHandler(EventStateWriter state) => _state = state;

        protected override bool Execute(Announce action)
        {
            _state.Announce(action.Text);
            return true;
        }
    }
}
