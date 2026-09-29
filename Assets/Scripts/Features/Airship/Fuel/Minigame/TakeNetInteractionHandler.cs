#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Features.Carry;
using TinCan.Core.Interaction;
using TinCan.Core.Items;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>
    /// Server-side handler for IA_TakeNet: empty-handed takes the net (<see cref="FlyingCanConfig.NetItem"/>),
    /// holding the net hangs it back, holding anything else is refused.
    /// </summary>
    public class TakeNetInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Carry";

        private readonly IEventPublisher _eventPublisher;
        private readonly FlyingCanConfig _config;

        public TakeNetInteractionHandler(IEventPublisher eventPublisher, FlyingCanConfig config)
        {
            _eventPublisher = eventPublisher;
            _config = config;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is not INetRack) return;

            var equipment = EquipmentLocator.Resolve(context.Requester);
            if (equipment == null) return;

            var net = _config.NetItem;
            if (net == null)
            {
                _eventPublisher.LogWarning(LogSource, "FlyingCanConfig has no NetItem; the rack has nothing to hand out.");
                return;
            }

            switch (holdingNet: equipment.IsHolding(net), emptyHanded: equipment.IsEmptyHanded)
            {
                case (holdingNet: true, _):
                    equipment.TryUnequip();
                    _eventPublisher.LogInfo(LogSource, "Net returned to the rack.");
                    break;
                case (_, emptyHanded: true):
                    equipment.TryEquip(net);
                    _eventPublisher.LogInfo(LogSource, "Net taken.");
                    break;
                default:
                    _eventPublisher.LogInfo(LogSource, $"Hands full ({equipment.Held?.DisplayName}); cannot take the net.");
                    break;
            }
        }
    }
}
