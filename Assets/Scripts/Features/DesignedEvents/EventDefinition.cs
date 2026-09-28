#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// An immutable designed event: phases run in order, then the success actions; a phase that times out on its
    /// condition runs the failure actions instead. Authored in code with <see cref="Builder"/> (see <see cref="EventCatalog"/>).
    /// </summary>
    public sealed class EventDefinition
    {
        private EventDefinition(int id, string name, string description, IReadOnlyList<EventPhase> phases,
            IReadOnlyList<IEventAction> successActions, IReadOnlyList<IEventAction> failureActions)
        {
            Id = id;
            Name = name;
            Description = description;
            Phases = phases;
            SuccessActions = successActions;
            FailureActions = failureActions;
        }

        public int Id { get; }
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<EventPhase> Phases { get; }
        public IReadOnlyList<IEventAction> SuccessActions { get; }
        public IReadOnlyList<IEventAction> FailureActions { get; }

        /// <summary>Every action and condition in the event, for handler checks.</summary>
        public IEnumerable<object> Steps => Phases
            .SelectMany(phase => phase.EnterActions.Cast<object>().Concat(phase.EndCondition != null ? new object[] { phase.EndCondition } : Array.Empty<object>()))
            .Concat(SuccessActions)
            .Concat(FailureActions);

        public override string ToString() => $"{Name} (#{Id})";

        /// <summary>Fluent authoring. <see cref="Build"/> validates and freezes; it throws on a definition that cannot run.</summary>
        public sealed class Builder
        {
            private readonly int _id;
            private readonly string _name;
            private string _description = string.Empty;
            private readonly List<EventPhase> _phases = new();
            private readonly List<IEventAction> _success = new();
            private readonly List<IEventAction> _failure = new();

            public Builder(int id, string name)
            {
                _id = id;
                _name = name;
            }

            public Builder Describe(string description)
            {
                _description = description;
                return this;
            }

            public Builder Phase(string name, Func<PhaseBuilder, PhaseBuilder> configure)
            {
                _phases.Add(configure(new PhaseBuilder(name)).Build());
                return this;
            }

            public Builder OnSuccess(params IEventAction[] actions)
            {
                _success.AddRange(actions);
                return this;
            }

            public Builder OnFailure(params IEventAction[] actions)
            {
                _failure.AddRange(actions);
                return this;
            }

            public EventDefinition Build()
            {
                string label = string.IsNullOrWhiteSpace(_name) ? $"event #{_id}" : $"event '{_name}'";
                if (_id <= 0) throw new InvalidOperationException($"{label}: id must be positive.");
                if (string.IsNullOrWhiteSpace(_name)) throw new InvalidOperationException($"{label}: needs a name.");
                if (_phases.Count == 0) throw new InvalidOperationException($"{label}: needs at least one phase.");

                var duplicate = _phases.GroupBy(phase => phase.Name).FirstOrDefault(group => group.Count() > 1);
                if (duplicate != null) throw new InvalidOperationException($"{label}: phase name '{duplicate.Key}' is used twice.");

                var endless = _phases.FirstOrDefault(phase => phase.Seconds <= 0f);
                if (endless != null)
                {
                    throw new InvalidOperationException(endless.EndCondition == null
                        ? $"{label}: phase '{endless.Name}' has no condition and no Duration."
                        : $"{label}: phase '{endless.Name}' has a condition but no Timeout.");
                }

                return new EventDefinition(_id, _name, _description, _phases.ToArray(), _success.ToArray(), _failure.ToArray());
            }
        }

        /// <summary>One phase: enter actions, then either a fixed duration or a condition with a timeout.</summary>
        public sealed class PhaseBuilder
        {
            private readonly string _name;
            private readonly List<IEventAction> _enter = new();
            private IEventCondition? _until;
            private float _seconds;

            public PhaseBuilder(string name) => _name = name;

            public PhaseBuilder OnEnter(params IEventAction[] actions)
            {
                _enter.AddRange(actions);
                return this;
            }

            /// <summary>The phase lasts this long (no condition).</summary>
            public PhaseBuilder Duration(float seconds)
            {
                _seconds = seconds;
                return this;
            }

            /// <summary>The phase ends when the condition holds; <see cref="Timeout"/> fails the event otherwise.</summary>
            public PhaseBuilder Until(IEventCondition condition)
            {
                _until = condition;
                return this;
            }

            public PhaseBuilder Timeout(float seconds)
            {
                _seconds = seconds;
                return this;
            }

            internal EventPhase Build() => new(_name, _enter.ToArray(), _until, _seconds);
        }
    }
}
