#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;
using TinCan.Core.Items;

namespace TinCan.Features.Gathering
{
    /// <summary>
    /// Server-side handler for IA_MineCrystal, run when the held work is done (its hold time is the mining). Empty-handed,
    /// a crystal breaks off into your hands; a bare vein gives nothing (it grows crystals back in time); with your hands
    /// full you cannot mine.
    /// </summary>
    public class MineCrystalInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Gathering";

        private readonly IEventPublisher _events;

        public MineCrystalInteractionHandler(IEventPublisher events) => _events = events;

        public void Handle(InteractionContext context)
        {
            if (context.Target is not IVein vein) return;

            var equipment = EquipmentLocator.Resolve(context.Requester);
            if (equipment == null) return;

            var crystal = vein.Config != null ? vein.Config.CrystalItem : null;
            if (crystal == null)
            {
                _events.LogWarning(LogSource, "GatheringConfig has no CrystalItem; veins give nothing.");
                return;
            }

            if (!equipment.IsEmptyHanded)
            {
                _events.LogInfo(LogSource, $"Hands full ({equipment.Held?.DisplayName}); cannot mine.");
                return;
            }

            if (!vein.TryTake())
            {
                _events.LogInfo(LogSource, "This vein is bare; crystals grow back in time.");
                return;
            }

            if (!equipment.TryEquip(crystal))
            {
                vein.Return();
                return;
            }

            _events.Publish(new CrystalMinedEvent(context.Requester.Id, vein.Available));
        }
    }
}
