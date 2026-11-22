#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;
using TinCan.Core.Items;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Server-side handler for IA_StokeFire (campfires and the ship's hearth). Holding firewood feeds it to the fire, which
    /// burns a log's worth longer; the log is used up only if the fire took it.
    /// </summary>
    public class StokeFireInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Camping";

        private readonly IEventPublisher _eventPublisher;

        public StokeFireInteractionHandler(IEventPublisher eventPublisher)
        {
            _eventPublisher = eventPublisher;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is not IFire fire || fire.Config is not { } config) return;

            var equipment = EquipmentLocator.Resolve(context.Requester);
            if (equipment == null) return;

            switch (holdingWood: equipment.IsHolding(config.FirewoodItem), emptyHanded: equipment.IsEmptyHanded)
            {
                case (holdingWood: true, _):
                    Stoke(context, equipment, fire, config);
                    break;
                case (_, emptyHanded: true):
                    _eventPublisher.LogInfo(LogSource, fire.IsLit ? "The fire burns; bring firewood to keep it going." : "The fire needs firewood.");
                    break;
                default:
                    _eventPublisher.LogInfo(LogSource, $"Cannot feed the fire {equipment.Held?.DisplayName}.");
                    break;
            }
        }

        private void Stoke(InteractionContext context, IEquipment equipment, IFire fire, CampingConfig config)
        {
            if (fire.Stoke(config.BurnSecondsPerLog) <= 0f)
            {
                _eventPublisher.LogInfo(LogSource, "The fire is already full.");
                return;
            }

            equipment.TryUnequip();
            _eventPublisher.Publish(new FireStokedEvent(context.Requester.Id, fire.BurnSecondsLeft));
        }
    }
}
