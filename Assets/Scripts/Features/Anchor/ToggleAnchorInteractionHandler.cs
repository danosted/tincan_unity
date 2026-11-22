#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;

namespace TinCan.Features.Anchor
{
    /// <summary>Server: Interact on an anchor (IA_ToggleAnchor) drops it when it is up and raises it when it is down.</summary>
    public sealed class ToggleAnchorInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Anchor";

        private readonly IEventPublisher _events;

        public ToggleAnchorInteractionHandler(IEventPublisher events) => _events = events;

        public void Handle(InteractionContext context)
        {
            if (context.Target is not IAnchor anchor) return;

            bool down = !anchor.IsDown;
            anchor.ServerSetDown(down);
            _events.LogInfo(LogSource, down ? "Anchor dropped: the ship holds." : "Anchor raised: the ship is free.");
        }
    }
}
