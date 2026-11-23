#nullable enable
using System;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Features.Encounters;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for encounter zones (plan: encounters.md, E1). Commands (server): move the zone so the ship is inside
    /// it ("in", half its radius from the centre) or well outside ("out"), which stands in for the ship sailing in or out.
    /// Probes: a zone has spawned, and its replicated state (Idle, Warning, Running, Resting, Gone).
    /// </summary>
    public sealed class EncounterScenarioLibrary : IScenarioLibrary
    {
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;

        public EncounterScenarioLibrary(INetworkService network, IActorRegistry actors)
        {
            _network = network;
            _actors = actors;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlaceZone", Place)
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("ZoneSpawned", _ => Zone() is { } zone ? ScenarioCheck.Pass($"{zone.DisplayName} at {zone.Centre}") : ScenarioCheck.Fail("no encounter zone")),
            new ScenarioProbe("ZoneState", CheckState)
        };

        private ScenarioCheck Place(string where)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var zone = Zone();
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(s => s.IsSimulating && s.Transform != null);
            if (zone == null || ship == null) return ScenarioCheck.Fail("no zone or no ship");

            var at = new Vector2(ship.Transform.position.x, ship.Transform.position.z);
            float offset = where == "in" ? zone.Radius * 0.5f : zone.Radius * 3f;
            zone.ServerSetCentre(at + Vector2.up * offset);
            return ScenarioCheck.Pass($"zone moved {offset:0} m ahead of the ship: the ship is {(where == "in" ? "inside" : "outside")}");
        }

        private ScenarioCheck CheckState(string expected)
        {
            if (Zone() is not { } zone) return ScenarioCheck.Fail("no encounter zone");
            string detail = $"{zone.DisplayName} is {zone.State}";
            return string.Equals(zone.State.ToString(), expected, StringComparison.OrdinalIgnoreCase)
                ? ScenarioCheck.Pass(detail)
                : ScenarioCheck.Fail(detail);
        }

        private IEncounterZone? Zone() => _actors.GetActors<IEncounterZone>().FirstOrDefault(zone => zone.IsSimulating);
    }
}
