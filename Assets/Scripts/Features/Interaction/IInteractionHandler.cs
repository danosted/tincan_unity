using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Features.Interaction
{
    public readonly struct InteractionContext
    {
        public readonly IActor Requester;
        public readonly IInteractable Target;
        public readonly InteractionDefinition Definition;

        public InteractionContext(
            IActor requester,
            IInteractable target,
            InteractionDefinition definition)
        {
            Requester = requester;
            Target = target;
            Definition = definition;
        }
    }

    public interface IInteractionHandler
    {
        GameplayTag Tag { get; }
        void Handle(InteractionContext context);
    }

    public interface IInteractionHandlerRegistry
    {
        bool TryGetHandler(GameplayTag handlerTag, out IInteractionHandler handler);
    }
}
