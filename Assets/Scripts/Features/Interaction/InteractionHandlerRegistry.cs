using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Features.Interaction
{
    public class InteractionHandlerRegistry : IInteractionHandlerRegistry
    {
        private readonly Dictionary<GameplayTag, IInteractionHandler> _handlers;

        public InteractionHandlerRegistry(IEnumerable<IInteractionHandler> handlers)
        {
            _handlers = new Dictionary<GameplayTag, IInteractionHandler>();
            foreach (var handler in handlers)
            {
                if (handler.Tag != null)
                {
                    _handlers[handler.Tag] = handler;
                }
            }
        }

        public bool TryGetHandler(GameplayTag handlerTag, out IInteractionHandler handler)
        {
            handler = null!;
            return handlerTag != null && _handlers.TryGetValue(handlerTag, out handler);
        }
    }
}
