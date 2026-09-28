#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Networking;
using TinCan.Features.DesignedEvents;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for designed events. The command (server) starts a catalog event by id. Probes read the director,
    /// whose state is server-local in the POC, so they only answer on the server.
    /// </summary>
    public sealed class DesignedEventsScenarioLibrary : IScenarioLibrary
    {
        private readonly INetworkService _network;
        private readonly IEventDirector _director;

        public DesignedEventsScenarioLibrary(INetworkService network, IEventDirector director)
        {
            _network = network;
            _director = director;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("StartEvent", StartEvent)
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("EventPhase", CheckPhase),
            new ScenarioProbe("EventOutcome", CheckOutcome)
        };

        private ScenarioCheck StartEvent(string id)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            if (!int.TryParse(id, out int eventId)) return ScenarioCheck.Fail($"'{id}' is not an event id");
            return _director.TryStart(eventId, out var reason) ? ScenarioCheck.Pass(reason) : ScenarioCheck.Fail(reason);
        }

        private ScenarioCheck CheckPhase(string phase)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe (event state is not replicated yet)");
            string current = _director.ActivePhase?.Name ?? "none";
            string detail = $"{_director.Active?.Name ?? "no event"} phase {current}";
            return current == phase ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckOutcome(string outcome)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe (event state is not replicated yet)");
            string detail = $"{_director.LastFinished?.Name ?? "no event"} {_director.LastOutcome}";
            return string.Equals(_director.LastOutcome.ToString(), outcome, StringComparison.OrdinalIgnoreCase)
                ? ScenarioCheck.Pass(detail)
                : ScenarioCheck.Fail(detail);
        }
    }
}
