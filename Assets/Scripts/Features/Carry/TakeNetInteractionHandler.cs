#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Features.Interaction;

namespace TinCan.Features.Carry
{
    /// <summary>
    /// Server-side handler for IA_TakeNet: empty-handed takes the net, carrying the net hangs it back,
    /// carrying anything else is refused.
    /// </summary>
    public class TakeNetInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Carry";

        private readonly IEventPublisher _eventPublisher;

        public TakeNetInteractionHandler(IEventPublisher eventPublisher)
        {
            _eventPublisher = eventPublisher;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is not INetRack) return;

            var carrier = CarrierLocator.Resolve(context.Requester);
            if (carrier == null) return;

            switch (carrier.Carried)
            {
                case CarriedItem.Net:
                    carrier.TryDrop();
                    _eventPublisher.LogInfo(LogSource, "Net returned to the rack.");
                    break;
                case CarriedItem.None:
                    carrier.TryPickUp(CarriedItem.Net);
                    _eventPublisher.LogInfo(LogSource, "Net taken.");
                    break;
                default:
                    _eventPublisher.LogInfo(LogSource, $"Hands full ({carrier.Carried}); cannot take the net.");
                    break;
            }
        }
    }
}
