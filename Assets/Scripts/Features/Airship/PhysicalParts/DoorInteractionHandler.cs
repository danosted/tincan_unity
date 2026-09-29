#nullable enable
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Interaction;

namespace TinCan.Features.Airship.PhysicalParts
{
    /// <summary>Toggles an <see cref="AirshipDoor"/> on the server when a player interacts with it (IA_ToggleDoor).</summary>
    public class DoorInteractionHandler : IInteractionHandler
    {
        public DoorInteractionHandler(GameplayTag? handlerTag) => Tag = handlerTag!;

        public GameplayTag Tag { get; }

        public void Handle(InteractionContext context)
        {
            if (context.Target is AirshipDoor door) door.ServerToggle();
        }
    }
}
