#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Input;
using VContainer.Unity;

namespace TinCan.Core.Input
{
    /// <summary>
    /// Application Layer, every frame: the one place discrete actions become commands. For each action pressed this
    /// frame it walks the live contexts from the top; the first route whose handler accepts the command consumes the
    /// action, so Cancel closes the menu or leaves the helm, never both. Handlers are found by command type.
    /// </summary>
    public sealed class InputRoutingUseCase : ITickable
    {
        private const string LogSource = "Input";

        private readonly IInputContexts _contexts;
        private readonly IInputReader _reader;
        private readonly IEventPublisher _events;
        private readonly Dictionary<Type, IInputCommandHandler> _handlers = new();
        private readonly HashSet<InputActionId> _consumed = new();
        private readonly HashSet<Type> _reportedMissing = new();

        public InputRoutingUseCase(IInputContexts contexts, IInputReader reader, IReadOnlyList<IInputCommandHandler> handlers, IEventPublisher events)
        {
            _contexts = contexts;
            _reader = reader;
            _events = events;

            foreach (var handler in handlers)
            {
                if (_handlers.TryAdd(handler.CommandType, handler)) continue;
                _events.LogWarning(LogSource, $"Two handlers for {handler.CommandType.Name}: {_handlers[handler.CommandType].GetType().Name} and {handler.GetType().Name}. The first wins.");
            }
        }

        /// <summary>The handler that owns a command type, for the Input Map report.</summary>
        public IInputCommandHandler? HandlerFor(Type commandType) => _handlers.TryGetValue(commandType, out var handler) ? handler : null;

        public void Tick()
        {
            _consumed.Clear();
            foreach (var context in _contexts.Active)
            {
                foreach (var route in context.Routes)
                {
                    if (route.Action == null || route.Command == null || _consumed.Contains(route.Action)) continue;
                    if (!_reader.WasPressedThisFrame(route.Action)) continue;
                    if (TryRun(route.Command)) _consumed.Add(route.Action);
                }
            }
        }

        private bool TryRun(InputCommand command)
        {
            var type = command.GetType();
            if (_handlers.TryGetValue(type, out var handler)) return handler.TryHandle(command);

            if (_reportedMissing.Add(type)) _events.LogWarning(LogSource, $"No handler registered for {type.Name} ({command.name}).");
            return false;
        }
    }
}
