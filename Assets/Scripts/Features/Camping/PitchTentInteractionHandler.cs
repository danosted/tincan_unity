#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;
using TinCan.Core.Items;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Server-side handler for IA_PitchTent. Holding a tent roll at a free spot pitches the tent; empty-handed at a
    /// pitched tent packs it back into a roll; a tent a player set down is then taken away. Nothing is lost: a failed
    /// equip leaves the tent standing.
    /// </summary>
    public class PitchTentInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Camping";

        private readonly IEventPublisher _eventPublisher;

        public PitchTentInteractionHandler(IEventPublisher eventPublisher)
        {
            _eventPublisher = eventPublisher;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is not ITentSpot spot) return;

            var equipment = EquipmentLocator.Resolve(context.Requester);
            if (equipment == null) return;

            var tent = spot.Config != null ? spot.Config.TentItem : null;
            if (tent == null)
            {
                _eventPublisher.LogWarning(LogSource, "CampingConfig has no TentItem; tents cannot be pitched.");
                return;
            }

            switch (pitched: spot.IsPitched, holdingTent: equipment.IsHolding(tent), emptyHanded: equipment.IsEmptyHanded)
            {
                case (pitched: false, holdingTent: true, _) when spot.TryPitch():
                    equipment.TryUnequip();
                    _eventPublisher.Publish(new TentPitchedEvent(context.Requester.Id));
                    break;
                case (pitched: true, _, emptyHanded: true) when spot.TryPack():
                    if (!equipment.TryEquip(tent))
                    {
                        spot.TryPitch();
                        break;
                    }
                    if (spot.RemovesWhenPacked) spot.Remove();
                    _eventPublisher.Publish(new TentPackedEvent(context.Requester.Id));
                    break;
                case (pitched: false, _, _):
                    _eventPublisher.LogInfo(LogSource, "A good spot for a tent; bring a tent roll from the ship.");
                    break;
                default:
                    _eventPublisher.LogInfo(LogSource, $"Hands full ({equipment.Held?.DisplayName}); cannot pack the tent.");
                    break;
            }
        }
    }
}
