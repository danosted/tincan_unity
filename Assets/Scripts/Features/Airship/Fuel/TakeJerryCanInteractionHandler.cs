#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Features.Interaction;
using TinCan.Features.Items;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Server-side handler for IA_TakeJerryCan. Toggle semantics: empty-handed takes a can from the crate,
    /// holding a can returns it, holding anything else is refused. Nothing can get stuck.
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

            var equipment = EquipmentLocator.Resolve(context.Requester);
            if (equipment == null) return;

            var can = supply.Item;
            if (can == null)
            {
                _eventPublisher.LogWarning(LogSource, "FuelConfig has no JerryCanItem; cannot hand out cans.");
                return;
            }

            switch (holdingCan: equipment.IsHolding(can), emptyHanded: equipment.IsEmptyHanded)
            {
                case (holdingCan: true, _) when equipment.TryUnequip():
                    supply.Add(1);
                    _eventPublisher.Publish(new JerryCanReturnedEvent(context.Requester.Id, supply.Count));
                    break;
                case (_, emptyHanded: true) when supply.TryTake():
                    if (!equipment.TryEquip(can))
                    {
                        supply.Add(1);
                        break;
                    }
                    _eventPublisher.Publish(new JerryCanTakenEvent(context.Requester.Id, supply.Count));
                    break;
                case (_, emptyHanded: true):
                    _eventPublisher.LogInfo(LogSource, "Jerry can supply is empty.");
                    break;
                default:
                    _eventPublisher.LogInfo(LogSource, $"Hands full ({equipment.Held?.DisplayName}); cannot take a jerry can.");
                    break;
            }
        }
    }
}
