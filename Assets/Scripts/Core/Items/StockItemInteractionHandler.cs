#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;

namespace TinCan.Core.Items
{
    /// <summary>
    /// Server: Interact on a stockpile (<see cref="IItemStockpile"/>). Holding its item puts it in, empty hands take one
    /// out; a full store, an empty one, or another item in hand changes nothing.
    /// </summary>
    public sealed class StockItemInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Items";

        private readonly IEventPublisher _events;

        public StockItemInteractionHandler(IEventPublisher events) => _events = events;

        public void Handle(InteractionContext context)
        {
            if (context.Target is not IItemStockpile store || store.Item is not { } item) return;

            var equipment = EquipmentLocator.Resolve(context.Requester);
            if (equipment == null) return;

            if (equipment.IsHolding(item))
            {
                if (!store.ServerTryAdd()) _events.LogInfo(LogSource, $"The store is full ({store.Capacity}).");
                else equipment.TryUnequip();
                return;
            }

            if (!equipment.IsEmptyHanded)
            {
                _events.LogInfo(LogSource, $"Hands full ({equipment.Held?.DisplayName}); this store takes {item.DisplayName}.");
                return;
            }

            if (store.Count <= 0)
            {
                _events.LogInfo(LogSource, $"The store holds no {item.DisplayName}.");
                return;
            }

            if (equipment.TryEquip(item)) store.ServerTryTake();
        }
    }
}
