#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Application Layer, server only, after airship movement. Runs one designed event at a time: enters a phase (runs
    /// its actions), asks <see cref="EventRunProcessor"/> each tick whether to stay, move on or finish, then runs the
    /// success or failure actions. With <see cref="AutoStart"/> it starts the catalog's events in rotation, with a
    /// quiet gap between them. Time is in simulation ticks, converted from the authored seconds when a phase starts.
    /// POC: state is server-local (no replication yet).
    /// </summary>
    public sealed class EventDirectorUseCase : ISimulationTickable, IEventDirector
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "Events";

        private readonly INetworkService _network;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly EventHandlerRegistry _handlers;
        private readonly EventRunProcessor _processor;
        private readonly EventDirectorSettings _settings;
        private readonly IReadOnlyList<EventDefinition> _catalog;

        private int _phaseIndex;
        private int _phaseStartTick;
        private int _phaseTicks;
        private int _nextAutoStartTick = -1;
        private int _rotation;

        public EventDirectorUseCase(INetworkService network, ITimeService time, IEventPublisher events,
            EventHandlerRegistry handlers, EventRunProcessor processor, EventDirectorSettings settings,
            IReadOnlyList<EventDefinition> catalog)
        {
            _network = network;
            _time = time;
            _events = events;
            _handlers = handlers;
            _processor = processor;
            _settings = settings;
            _catalog = catalog;
            AutoStart = settings.AutoStart;
        }

        public bool AutoStart { get; set; }
        public EventDefinition? Active { get; private set; }
        public EventPhase? ActivePhase => Active?.Phases[_phaseIndex];
        public EventDefinition? LastFinished { get; private set; }
        public EventOutcome LastOutcome { get; private set; }

        public void Tick()
        {
            if (!_network.IsServer) return;

            int now = _time.Tick;
            if (Active == null)
            {
                TryAutoStart(now);
                return;
            }

            var phase = Active.Phases[_phaseIndex];
            bool met = phase.EndCondition != null && _handlers.IsMet(phase.EndCondition);
            var step = _processor.Advance(phase.EndCondition != null, met, now - _phaseStartTick, _phaseTicks,
                _phaseIndex == Active.Phases.Count - 1);

            switch (step)
            {
                case EventStep.NextPhase:
                    EnterPhase(_phaseIndex + 1, now);
                    break;
                case EventStep.Succeed:
                    Finish(EventOutcome.Succeeded, now);
                    break;
                case EventStep.Fail:
                    Finish(EventOutcome.Failed, now);
                    break;
            }
        }

        public bool TryStart(int eventId, out string reason)
        {
            var definition = _catalog.FirstOrDefault(candidate => candidate.Id == eventId);
            reason = (_network.IsServer, Active, definition) switch
            {
                (false, _, _) => "server only",
                (_, not null, _) => $"{Active} is running",
                (_, _, null) => $"no event with id {eventId}",
                _ => MissingHandlersReason(definition!)
            };
            if (reason.Length > 0) return false;

            Active = definition!;
            _events.LogInfo(LogSource, $"{definition} started.");
            EnterPhase(0, _time.Tick);
            reason = $"{definition} started";
            return true;
        }

        private void TryAutoStart(int now)
        {
            if (!AutoStart || _catalog.Count == 0) return;
            if (_nextAutoStartTick < 0) _nextAutoStartTick = now + GameplayTicks.FromSeconds(_settings.FirstEventDelay, _time.TickRate);
            if (now < _nextAutoStartTick) return;

            // One attempt per catalog entry; skip events this profile cannot run.
            string reason = string.Empty;
            for (int i = 0; i < _catalog.Count; i++)
            {
                var candidate = _catalog[_rotation++ % _catalog.Count];
                if (TryStart(candidate.Id, out reason)) return;
            }

            _events.LogWarning(LogSource, $"No catalog event can start in this profile (last: {reason}).");
            _nextAutoStartTick = now + GameplayTicks.FromSeconds(_settings.QuietGap, _time.TickRate);
        }

        private void EnterPhase(int index, int now)
        {
            _phaseIndex = index;
            _phaseStartTick = now;
            var phase = Active!.Phases[index];
            _phaseTicks = GameplayTicks.FromSeconds(phase.Seconds, _time.TickRate);
            _events.LogInfo(LogSource, $"{Active} phase '{phase.Name}' ({phase.Seconds:0.#} s).");
            Run(phase.EnterActions);
        }

        private void Finish(EventOutcome outcome, int now)
        {
            var finished = Active!;
            Active = null;
            _phaseIndex = 0;
            LastFinished = finished;
            LastOutcome = outcome;
            _events.LogInfo(LogSource, $"{finished} {outcome.ToString().ToLowerInvariant()}.");
            Run(outcome == EventOutcome.Succeeded ? finished.SuccessActions : finished.FailureActions);
            _nextAutoStartTick = now + GameplayTicks.FromSeconds(_settings.QuietGap, _time.TickRate);
        }

        private void Run(IEnumerable<IEventAction> actions)
        {
            foreach (var action in actions)
            {
                if (!_handlers.TryExecute(action)) _events.LogWarning(LogSource, $"{Active ?? LastFinished}: '{action}' could not be applied.");
            }
        }

        private string MissingHandlersReason(EventDefinition definition)
        {
            var missing = _handlers.MissingHandlers(definition);
            return missing.Count == 0 ? string.Empty : $"{definition} needs handlers this profile lacks: {string.Join(", ", missing.Select(type => type.Name))}";
        }
    }
}
