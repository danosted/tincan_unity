#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Features.ShipLoss;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the ship lost and brought back, with or without a voyage.
    /// <list type="bullet">
    /// <item>Server commands hurt the ship or drop it deep into the cloud (lost to the cloud boundary once its countdown
    /// runs out).</item>
    /// <item>Probes read the replicated loss state and the ship on this peer, so they answer on host and client alike:
    /// <c>ShipLost</c> ("true" or "false"), <c>ShipHealthFull</c>, <c>ShipBackWhereItSank</c>.</item>
    /// </list>
    /// Plan: .docs/plans/ship-lost-and-respawn.md.
    /// </summary>
    public sealed class ShipLossScenarioLibrary : IScenarioLibrary
    {
        /// <summary>Far below the test seas (a swell of ±20 m about 0; the test areas used have no holes under the ship).</summary>
        private const float DeepInCloud = -300f;

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IShipLoss _loss;
        private Vector3? _sankFrom;

        public ShipLossScenarioLibrary(INetworkService network, IActorRegistry actors, IShipLoss loss)
        {
            _network = network;
            _actors = actors;
            _loss = loss;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("HurtShip", _ => ServerOnly(Hurt, "ship health halved")),
            new ScenarioCommand("SinkShipInCloud", _ => ServerOnly(SinkInCloud, $"ship dropped to {DeepInCloud:0} m, far below the cloud surface"))
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("ShipLost", CheckLost),
            new ScenarioProbe("ShipHealthFull", _ => CheckHealthFull()),
            new ScenarioProbe("ShipBackWhereItSank", _ => CheckBack())
        };

        private ScenarioCheck ServerOnly(Func<bool> action, string success)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            return action() ? ScenarioCheck.Pass(success) : ScenarioCheck.Fail("no ship, or it has no health");
        }

        private bool Hurt()
        {
            var controller = (Ship() as IShipState)?.Controller;
            if (controller == null || !controller.TryGetHealth(out var health)) return false;
            controller.SetAttribute(health.HealthDef, new AttributeValue(health.MaxHealth * 0.5f));
            return true;
        }

        // The ship was flying safely above the sea until now, so this is the last safe spot it respawns at.
        private bool SinkInCloud()
        {
            var ship = Ship();
            if (ship == null) return false;
            var from = ship.Transform.position;
            _sankFrom = from;
            var to = new Vector3(from.x, DeepInCloud, from.z);
            if (ship is IAirshipTeleport teleport) teleport.Teleport(to, ship.Transform.rotation);
            else ship.Transform.position = to;
            return true;
        }

        private ScenarioCheck CheckLost(string expected)
        {
            bool want = !string.Equals(expected, "false", StringComparison.OrdinalIgnoreCase);
            string detail = $"ship {(_loss.IsLost ? "lost" : "not lost")} on this peer";
            return _loss.IsLost == want ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckBack()
        {
            var ship = Ship();
            if (ship == null || _sankFrom == null) return ScenarioCheck.Fail("no ship, or it was not sunk on this peer");
            float off = Vector3.Distance(ship.Transform.position, _sankFrom.Value);
            string detail = $"ship {off:0.0} m from where it sank ({ship.Transform.position}) on this peer";
            return off < 5f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckHealthFull()
        {
            var controller = (Ship() as IShipState)?.Controller;
            if (!controller.TryGetHealth(out var health)) return ScenarioCheck.Fail("no ship health");
            string detail = $"ship health {health.Health:0} / {health.MaxHealth:0} on this peer";
            return health.MaxHealth > 0f && !health.IsDamaged ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private IAirshipView? Ship() => _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.Transform != null);
    }
}
