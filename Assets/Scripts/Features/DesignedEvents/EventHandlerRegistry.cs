#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Finds the handler for an action or condition by its type. Handlers come from every loaded feature's installer,
    /// so a scene only runs events whose steps its profile can handle (<see cref="MissingHandlers"/>).
    /// </summary>
    public sealed class EventHandlerRegistry
    {
        private readonly Dictionary<Type, IEventActionHandler> _actions;
        private readonly Dictionary<Type, IEventConditionHandler> _conditions;

        public EventHandlerRegistry(IEnumerable<IEventActionHandler> actions, IEnumerable<IEventConditionHandler> conditions)
        {
            _actions = actions.ToDictionary(handler => handler.ActionType);
            _conditions = conditions.ToDictionary(handler => handler.ConditionType);
        }

        public bool TryExecute(IEventAction action) =>
            _actions.TryGetValue(action.GetType(), out var handler) && handler.Execute(action);

        /// <summary>False without a handler; <see cref="MissingHandlers"/> keeps such events from starting.</summary>
        public bool IsMet(IEventCondition condition) =>
            _conditions.TryGetValue(condition.GetType(), out var handler) && handler.IsMet(condition);

        public IReadOnlyList<Type> MissingHandlers(EventDefinition definition) => definition.Steps
            .Select(step => step.GetType())
            .Distinct()
            .Where(type => !_actions.ContainsKey(type) && !_conditions.ContainsKey(type))
            .ToArray();
    }
}
