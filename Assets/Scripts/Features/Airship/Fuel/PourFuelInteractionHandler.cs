#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Features.Carry;
using TinCan.Features.Interaction;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Server-side handler for IA_PourFuel. A player carrying a jerry can pours it into the tank (the can is used
    /// up only if the tank accepted fuel). Without a can, the config's DebugFreeRefuel stopgap may still refuel.
    /// </summary>
    public class PourFuelInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Fuel";

        private readonly IEventPublisher _eventPublisher;

        public PourFuelInteractionHandler(IEventPublisher eventPublisher)
        {
            _eventPublisher = eventPublisher;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is not IFuelFillPort port || port.Tank is not { } tank || tank.Config is not { } config) return;

            var carrier = CarrierLocator.Resolve(context.Requester);
            switch (carrying: carrier?.Carried ?? CarriedItem.None, debug: config.DebugFreeRefuel)
            {
                case (carrying: CarriedItem.JerryCan, debug: _):
                    PourCarriedCan(context, carrier!, tank, config);
                    break;
                case (carrying: CarriedItem.None, debug: true):
                    Refill(context, tank, config.JerryCanLitres);
                    break;
                case (carrying: CarriedItem.None, debug: false):
                    _eventPublisher.LogInfo(LogSource, "Nothing to pour; fetch a jerry can first.");
                    break;
                default:
                    _eventPublisher.LogInfo(LogSource, $"Cannot pour while carrying {carrier!.Carried}.");
                    break;
            }
        }

        private void PourCarriedCan(InteractionContext context, ICarrier carrier, IFuelTank tank, FuelConfig config)
        {
            if (!Refill(context, tank, config.JerryCanLitres)) return;
            carrier.TryDrop();
        }

        private bool Refill(InteractionContext context, IFuelTank tank, float litres)
        {
            float accepted = tank.Refill(litres);
            if (accepted <= 0f)
            {
                _eventPublisher.LogInfo(LogSource, "Tank already full.");
                return false;
            }

            _eventPublisher.Publish(new FuelRefilledEvent(context.Requester.Id, accepted));
            return true;
        }
    }
}
