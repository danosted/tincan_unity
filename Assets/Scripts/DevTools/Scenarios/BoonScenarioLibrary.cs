#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Features.Boons;
using TinCan.Features.Gates;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for boon gates (hazards-and-boons.md, B1). Command: put a gate around the ship's body (server), so
    /// the next tick takes its boon. Probes: gates in the sky and gates passed (server), and gates this peer sees used
    /// (replicated).
    /// </summary>
    public sealed class BoonScenarioLibrary : IScenarioLibrary
    {
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IGates _gates;
        private readonly BoonsConfig _config;

        public BoonScenarioLibrary(INetworkService network, IActorRegistry actors, IGates gates, BoonsConfig config)
        {
            _network = network;
            _actors = actors;
            _gates = gates;            _config = config;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("SpawnBoonGateOnShip", _ => SpawnOnShip())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("BoonGatesLaid", CheckLaid),
            new ScenarioProbe("BoonsTaken", CheckTaken),
            new ScenarioProbe("BoonGatesUsedHere", CheckUsedHere)
        };

        /// <summary>Server: a gate at the middle of the ship's solid body, so the ship is inside it.</summary>
        private ScenarioCheck SpawnOnShip()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.Transform != null);
            if (ship == null) return ScenarioCheck.Fail("no ship");

            var root = (ship as Component)?.transform ?? ship.Transform;
            Bounds? body = null;
            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled) continue;
                if (body is { } bounds)
                {
                    bounds.Encapsulate(collider.bounds);
                    body = bounds;
                }
                else body = collider.bounds;
            }

            var at = body?.center ?? root.position;
            var gate = _config.GateAt(0);
            if (gate == null) return ScenarioCheck.Fail("no boon gate in BoonsConfig");
            return _gates.Spawn(gate, at) != null
                ? ScenarioCheck.Pass($"{gate.name} on the ship at {at}")
                : ScenarioCheck.Fail($"{gate.name} did not spawn (no prefab?)");
        }

        /// <summary>Server: at least this many gates are in the sky (a voyage laid them).</summary>
        private ScenarioCheck CheckLaid(string atLeast)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            int count = _gates.Gates.Count;
            return count >= Parse(atLeast) ? ScenarioCheck.Pass($"{count} gate(s)") : ScenarioCheck.Fail($"{count} gate(s)");
        }

        /// <summary>Server: exactly this many gates passed (a used gate must never give twice).</summary>
        private ScenarioCheck CheckTaken(string exactly)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            string detail = $"{_gates.Passed} gate(s) passed";
            return _gates.Passed == Parse(exactly) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Any peer: at least this many gates show as used here (the replicated state).</summary>
        private ScenarioCheck CheckUsedHere(string atLeast)
        {
            var gates = Object.FindObjectsByType<GateNetworkMediator>(FindObjectsSortMode.None);
            int used = gates.Count(gate => gate.IsUsed);
            string detail = $"{used} of {gates.Length} gate(s) used on this peer";
            return used >= Parse(atLeast) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private static int Parse(string value) => int.Parse(value, CultureInfo.InvariantCulture);
    }
}
