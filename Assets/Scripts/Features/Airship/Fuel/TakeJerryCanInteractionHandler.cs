#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Features.Carry;
using TinCan.Features.Interaction;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Server-side handler for IA_TakeJerryCan. Toggle semantics: empty-handed takes a can from the crate,
    /// carrying a can returns it, carrying anything else is refused. Nothing can get stuck.
    /// </summary>
    public class TakeJerryCanInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Fuel";

        private readonly IEventPublisher _eventPublisher;

        public TakeJerryCanInteractionHandler(IEventPublisher eventPublisher)
        {
            _eventPublisher = eventPublisher;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is not IJerryCanSupply supply) return;

            var carrier = CarrierLocator.Resolve(context.Requester);
            if (carrier == null) return;

            switch (carrier.Carried)
            {
                case CarriedItem.JerryCan when carrier.TryDrop():
                    supply.Add(1);
                    _eventPublisher.Publish(new JerryCanReturnedEvent(context.Requester.Id, supply.Count));
                    break;
                case CarriedItem.None when supply.TryTake():
                    if (!carrier.TryPickUp(CarriedItem.JerryCan))
                    {
                        supply.Add(1);
                        break;
                    }
                    _eventPublisher.Publish(new JerryCanTakenEvent(context.Requester.Id, supply.Count));
                    break;
                case CarriedItem.None:
                    _eventPublisher.LogInfo(LogSource, "Jerry can supply is empty.");
                    break;
                default:
                    _eventPublisher.LogInfo(LogSource, $"Hands full ({carrier.Carried}); cannot take a jerry can.");
                    break;
            }
        }
    }
}
