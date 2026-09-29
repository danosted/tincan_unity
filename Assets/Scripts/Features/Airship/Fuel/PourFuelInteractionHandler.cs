#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;
using TinCan.Core.Items;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Server-side handler for IA_PourFuel. A player holding a jerry can (FuelConfig.JerryCanItem) pours it into the tank (the can is used
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

            var equipment = EquipmentLocator.Resolve(context.Requester);
            bool holdingCan = equipment != null && equipment.IsHolding(config.JerryCanItem);
            bool emptyHanded = equipment == null || equipment.IsEmptyHanded;
            switch (holdingCan, emptyHanded, debug: config.DebugFreeRefuel)
            {
                case (holdingCan: true, _, _):
                    PourCarriedCan(context, equipment!, tank, config);
                    break;
                case (_, emptyHanded: true, debug: true):
                    Refill(context, tank, config.JerryCanLitres);
                    break;
                case (_, emptyHanded: true, debug: false):
                    _eventPublisher.LogInfo(LogSource, "Nothing to pour; fetch a jerry can first.");
                    break;
                default:
                    _eventPublisher.LogInfo(LogSource, $"Cannot pour while holding {equipment!.Held?.DisplayName}.");
                    break;
            }
        }

        private void PourCarriedCan(InteractionContext context, IEquipment equipment, IFuelTank tank, FuelConfig config)
        {
            if (!Refill(context, tank, config.JerryCanLitres)) return;
            equipment.TryUnequip();
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
