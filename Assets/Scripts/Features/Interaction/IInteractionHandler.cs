using System;
using TinCan.Core.Domain;

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
        void Handle(InteractionContext context);
    }

    public interface IInteractionHandlerRegistry
    {
        bool TryGetHandler(Type handlerType, out IInteractionHandler handler);
        bool TryGetHandler<THandler>(out THandler handler) where THandler : class, IInteractionHandler;
    }
}
