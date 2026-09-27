#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Features.Interaction;

namespace TinCan.Features.Items
{
    /// <summary>
    /// Server-side handler for item racks (IA_Take* assets pointing here). Toggle semantics: empty-handed takes the
    /// rack's item, holding that item puts it back, holding anything else is refused. The equipment change itself is
    /// replicated and published by the equipment mediator.
    /// </summary>
    public class TakeItemInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Items";

        private readonly IEventPublisher _eventPublisher;

        public TakeItemInteractionHandler(IEventPublisher eventPublisher)
        {
            _eventPublisher = eventPublisher;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is not IItemSource source) return;

            var equipment = EquipmentLocator.Resolve(context.Requester);
            if (equipment == null) return;

            var item = source.Item;
            if (item == null)
            {
                _eventPublisher.LogWarning(LogSource, "This rack has no item assigned.");
                return;
            }

            switch (holding: equipment.IsHolding(item), emptyHanded: equipment.IsEmptyHanded)
            {
                case (holding: true, _):
                    equipment.TryUnequip();
                    break;
                case (_, emptyHanded: true):
                    equipment.TryEquip(item);
                    break;
                default:
                    _eventPublisher.LogInfo(LogSource, $"Hands full ({equipment.Held?.DisplayName}); cannot take the {item.DisplayName}.");
                    break;
            }
        }
    }
}
