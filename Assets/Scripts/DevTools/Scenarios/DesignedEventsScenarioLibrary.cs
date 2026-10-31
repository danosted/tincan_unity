#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.DesignedEvents;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for designed events. The command (server) starts a catalog event by id. Probes read the director
    /// on the server and the replicated <see cref="IEventState"/> elsewhere (names come from the catalog every peer
    /// builds), so they answer on clients too.
    /// </summary>
    public sealed class DesignedEventsScenarioLibrary : IScenarioLibrary
    {
        private readonly INetworkService _network;
        private readonly IEventDirector _director;
        private readonly IActorRegistry _actors;
        private readonly IReadOnlyList<EventDefinition> _catalog;

        public DesignedEventsScenarioLibrary(INetworkService network, IEventDirector director, IActorRegistry actors,
            IReadOnlyList<EventDefinition> catalog)
        {
            _network = network;
            _director = director;
            _actors = actors;
            _catalog = catalog;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("StartEvent", StartEvent)
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("EventPhase", CheckPhase),
            new ScenarioProbe("EventOutcome", CheckOutcome),
            new ScenarioProbe("EventAnnounced", CheckAnnounced)
        };

        private ScenarioCheck StartEvent(string id)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            if (!int.TryParse(id, out int eventId)) return ScenarioCheck.Fail($"'{id}' is not an event id");
            return _director.TryStart(eventId, out var reason) ? ScenarioCheck.Pass(reason) : ScenarioCheck.Fail(reason);
        }

        private ScenarioCheck CheckPhase(string phase)
        {
            string current, eventName;
            if (_network.IsServer)
            {
                current = _director.ActivePhase?.Name ?? "none";
                eventName = _director.Active?.Name ?? "no event";
            }
            else
            {
                if (State() is not { } state) return ScenarioCheck.Fail("no replicated event state");
                var running = Definition(state.EventId);
                current = running != null && state.PhaseIndex < running.Phases.Count ? running.Phases[state.PhaseIndex].Name : "none";
                eventName = running?.Name ?? "no event";
            }

            string detail = $"{eventName} phase {current}";
            return current == phase ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckOutcome(string outcome)
        {
            EventOutcome last;
            string eventName;
            if (_network.IsServer)
            {
                last = _director.LastOutcome;
                eventName = _director.LastFinished?.Name ?? "no event";
            }
            else
            {
                if (State() is not { } state) return ScenarioCheck.Fail("no replicated event state");
                last = state.LastOutcome;
                eventName = Definition(state.LastEventId)?.Name ?? "no event";
            }

            string detail = $"{eventName} {last}";
            return string.Equals(last.ToString(), outcome, StringComparison.OrdinalIgnoreCase)
                ? ScenarioCheck.Pass(detail)
                : ScenarioCheck.Fail(detail);
        }

        /// <summary>The replicated announcement contains the text, on this peer.</summary>
        private ScenarioCheck CheckAnnounced(string text)
        {
            if (State() is not { } state) return ScenarioCheck.Fail("no replicated event state");
            string detail = $"announcement \"{state.Announcement}\"";
            return state.Announcement.Contains(text, StringComparison.OrdinalIgnoreCase) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private IEventState? State() => _actors.GetActors<IEventState>().FirstOrDefault(state => state.IsSimulating);

        private EventDefinition? Definition(int id) => id == 0 ? null : _catalog.FirstOrDefault(definition => definition.Id == id);
    }
}
