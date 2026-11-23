#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;
using UnityEngine;

namespace TinCan.Core.Items
{
    /// <summary>
    /// Server: the one handler for items between hands and a target, by what the target can do (ItemCapabilities.cs).
    /// In order:
    /// <list type="number">
    /// <item>holding an item the target accepts (<see cref="IItemConsumer"/>): it is used and leaves your hands;</item>
    /// <item>holding the target's own item (<see cref="IItemStore"/>): it goes back in;</item>
    /// <item>empty-handed (<see cref="IItemYield"/>): one comes out into your hands, or goes back if it cannot;</item>
    /// <item>anything else is refused, with a reason in the log.</item>
    /// </list>
    /// Publishes <see cref="ItemUsedEvent"/>, <see cref="ItemStoredEvent"/> and <see cref="ItemTakenEvent"/>. Held work
    /// (mining) is only the interaction's hold time (<see cref="InteractionDefinition.HoldSeconds"/>), not a handler.
    /// </summary>
    public sealed class ItemInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Items";

        private readonly IEventPublisher _events;

        public ItemInteractionHandler(IEventPublisher events) => _events = events;

        public void Handle(InteractionContext context)
        {
            var equipment = EquipmentLocator.Resolve(context.Requester);
            if (equipment == null) return;

            var target = context.Target;
            string name = Describe(target);
            var held = equipment.Held;

            if (held != null)
            {
                if (target is IItemConsumer consumer && consumer.Accepts(held))
                {
                    if (!consumer.TryConsume(held))
                    {
                        _events.LogInfo(LogSource, $"{name} cannot take the {held.DisplayName} now.");
                        return;
                    }

                    equipment.TryUnequip();
                    _events.Publish(new ItemUsedEvent(context.Requester.Id, held.name, name));
                    return;
                }

                if (target is IItemStore store && store.StoreItem == held)
                {
                    if (!store.TryStore())
                    {
                        _events.LogInfo(LogSource, $"{name} is full.");
                        return;
                    }

                    equipment.TryUnequip();
                    _events.Publish(new ItemStoredEvent(context.Requester.Id, held.name, name));
                    return;
                }

                _events.LogInfo(LogSource, $"Hands full ({held.DisplayName}); nothing to do with it at {name}.");
                return;
            }

            if (target is not IItemYield yield || yield.YieldItem is not { } item)
            {
                _events.LogInfo(LogSource, $"Nothing to take at {name}.");
                return;
            }

            if (!yield.TryYield())
            {
                _events.LogInfo(LogSource, $"No {item.DisplayName} left at {name}.");
                return;
            }

            if (!equipment.TryEquip(item))
            {
                yield.Unyield();
                return;
            }

            _events.Publish(new ItemTakenEvent(context.Requester.Id, item.name, name));
        }

        private static string Describe(object target) =>
            target is UnityEngine.Object unityObject && unityObject != null ? unityObject.name : target.GetType().Name;
    }
}
