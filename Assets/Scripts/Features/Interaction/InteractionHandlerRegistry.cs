using System;
using System.Collections.Generic;

namespace TinCan.Features.Interaction
{
    public class InteractionHandlerRegistry : IInteractionHandlerRegistry
    {
        private readonly Dictionary<Type, IInteractionHandler> _handlersByType;

        public InteractionHandlerRegistry(IEnumerable<IInteractionHandler> handlers)
        {
            _handlersByType = new Dictionary<Type, IInteractionHandler>();
            foreach (var handler in handlers)
            {
                _handlersByType[handler.GetType()] = handler;
            }
        }

        public bool TryGetHandler(Type handlerType, out IInteractionHandler handler)
        {
            handler = null!;
            return handlerType != null && _handlersByType.TryGetValue(handlerType, out handler);
        }

        public bool TryGetHandler<THandler>(out THandler handler) where THandler : class, IInteractionHandler
        {
            handler = null;
            return TryGetHandler(typeof(THandler), out var found) && (handler = found as THandler) != null;
        }
    }
}
