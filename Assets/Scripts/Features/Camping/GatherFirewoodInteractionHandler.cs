#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;
using TinCan.Core.Items;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Server-side handler for IA_GatherFirewood. Toggle semantics, like the crystal rack: empty-handed takes a log from
    /// the pile, holding firewood puts it back, holding anything else is refused. A bare pile gathers again in time.
    /// </summary>
    public class GatherFirewoodInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Camping";

        private readonly IEventPublisher _eventPublisher;

        public GatherFirewoodInteractionHandler(IEventPublisher eventPublisher)
        {
            _eventPublisher = eventPublisher;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is not IDeadwood pile) return;

            var equipment = EquipmentLocator.Resolve(context.Requester);
            if (equipment == null) return;

            var firewood = pile.Config != null ? pile.Config.FirewoodItem : null;
            if (firewood == null)
            {
                _eventPublisher.LogWarning(LogSource, "CampingConfig has no FirewoodItem; deadwood gives nothing.");
                return;
            }

            switch (holdingWood: equipment.IsHolding(firewood), emptyHanded: equipment.IsEmptyHanded)
            {
                case (holdingWood: true, _) when equipment.TryUnequip():
                    pile.Return();
                    break;
                case (_, emptyHanded: true) when pile.TryTake():
                    if (!equipment.TryEquip(firewood))
                    {
                        pile.Return();
                        break;
                    }
                    _eventPublisher.Publish(new FirewoodGatheredEvent(context.Requester.Id, pile.Available));
                    break;
                case (_, emptyHanded: true):
                    _eventPublisher.LogInfo(LogSource, "No dry wood left here; more falls in time.");
                    break;
                default:
                    _eventPublisher.LogInfo(LogSource, $"Hands full ({equipment.Held?.DisplayName}); cannot gather firewood.");
                    break;
            }
        }
    }
}
