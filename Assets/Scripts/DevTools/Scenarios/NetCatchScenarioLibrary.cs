#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Airship.Fuel.Minigame;
using TinCan.Core.Items;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the net-catch minigame: hand the subject a net, put a flying can in front of it, and check
    /// the catch on both sides (the can disappears for the subject; the ship's jerry-can supply grows on the server).
    /// </summary>
    public sealed class NetCatchScenarioLibrary : IScenarioLibrary
    {
        private readonly ScenarioSubject _subject;
        private readonly IActorRegistry _registry;
        private readonly INetworkService _network;
        private readonly IFlyingCanSpawner _spawner;
        private readonly CatchProcessor _catch;
        private readonly FlyingCanConfig _config;

        private int? _supplyBaseline;

        public NetCatchScenarioLibrary(
            ScenarioSubject subject,
            IActorRegistry registry,
            INetworkService network,
            IFlyingCanSpawner spawner,
            CatchProcessor catchProcessor,
            FlyingCanConfig config)
        {
            _subject = subject;
            _registry = registry;
            _network = network;
            _spawner = spawner;
            _catch = catchProcessor;
            _config = config;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("GiveSubjectNet", _ => GiveNet()),
            new ScenarioCommand("SpawnCanAtSubjectNet", _ => SpawnCan()),
            new ScenarioCommand("RecordJerryCanSupply", _ => RecordSupply())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("CanInNetReach", _ => CanInReach(expected: true)),
            new ScenarioProbe("NoCanInNetReach", _ => CanInReach(expected: false)),
            new ScenarioProbe("JerryCanSupplyIncreased", _ => SupplyIncreased())
        };

        private ScenarioCheck GiveNet()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");

            var equipment = EquipmentLocator.Resolve(_subject.Resolve());
            if (equipment == null) return ScenarioCheck.Fail("subject has no equipment");
            if (_config.NetItem == null) return ScenarioCheck.Fail("FlyingCanConfig has no NetItem");
            if (equipment.IsHolding(_config.NetItem)) return ScenarioCheck.Pass("already holding the net");
            if (!equipment.IsEmptyHanded) equipment.TryUnequip();

            return equipment.TryEquip(_config.NetItem)
                ? ScenarioCheck.Pass("net given")
                : ScenarioCheck.Fail($"equip refused while holding {equipment.Held?.name ?? "nothing"}");
        }

        private ScenarioCheck SpawnCan()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            if (!TryNetPosition(out var net)) return ScenarioCheck.Fail("no subject body");

            var can = _spawner.Spawn(net);
            return can != null
                ? ScenarioCheck.Pass($"can spawned at {Format(net)}")
                : ScenarioCheck.Fail("spawner returned no can (prefab missing?)");
        }

        private ScenarioCheck RecordSupply()
        {
            var supply = ResolveSupply();
            if (supply == null) return ScenarioCheck.Fail("no jerry-can supply on any airship");

            _supplyBaseline = supply.Count;
            return ScenarioCheck.Pass($"supply {supply.Count}");
        }

        private ScenarioCheck SupplyIncreased()
        {
            var supply = ResolveSupply();
            if (supply == null) return ScenarioCheck.Fail("no jerry-can supply on any airship");
            if (_supplyBaseline == null) return ScenarioCheck.Fail("RecordJerryCanSupply has not run");

            return supply.Count > _supplyBaseline
                ? ScenarioCheck.Pass($"supply {_supplyBaseline} -> {supply.Count}")
                : ScenarioCheck.Fail($"supply still {supply.Count}");
        }

        private ScenarioCheck CanInReach(bool expected)
        {
            if (!TryNetPosition(out var net)) return ScenarioCheck.Fail("no subject body");

            float nearest = float.PositiveInfinity;
            foreach (var can in _registry.GetActors<IFlyingCanView>())
            {
                if (can.Transform == null) continue;
                nearest = Math.Min(nearest, Vector3.Distance(can.Transform.position, net));
            }

            bool inReach = nearest <= _config.CatchRadius;
            string detail = float.IsPositiveInfinity(nearest) ? "no cans" : $"nearest can {nearest:0.00} m (radius {_config.CatchRadius:0.0})";
            return inReach == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private bool TryNetPosition(out Vector3 net)
        {
            net = default;
            var body = _subject.Body;
            if (body == null) return false;

            net = _catch.NetPosition(body.position, body.forward, _config.NetReach, _config.NetHeight);
            return true;
        }

        private IJerryCanSupply? ResolveSupply() =>
            _registry.GetActors<IAirshipView>()
                .Select(FuelTankLocator.FindFixture<IJerryCanSupply>)
                .FirstOrDefault(supply => supply != null);

        private static string Format(Vector3 v) => $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})";
    }
}
