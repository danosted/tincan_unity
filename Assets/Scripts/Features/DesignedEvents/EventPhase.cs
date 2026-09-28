#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// One step of a designed event. Without an end condition it lasts <see cref="Seconds"/>; with one it ends as soon
    /// as the condition holds and fails the event when <see cref="Seconds"/> run out. Built by <see cref="EventDefinition.Builder"/>.
    /// </summary>
    public sealed class EventPhase
    {
        public EventPhase(string name, IReadOnlyList<IEventAction> enterActions, IEventCondition? endCondition, float seconds)
        {
            Name = name;
            EnterActions = enterActions;
            EndCondition = endCondition;
            Seconds = seconds;
        }

        public string Name { get; }
        public IReadOnlyList<IEventAction> EnterActions { get; }
        public IEventCondition? EndCondition { get; }

        /// <summary>Duration without a condition, timeout with one. Authored in seconds; the director converts to ticks.</summary>
        public float Seconds { get; }
    }
}
