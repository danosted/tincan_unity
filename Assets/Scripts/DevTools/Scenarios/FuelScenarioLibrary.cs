#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Features.Airship.Fuel;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the ship's fuel tank: drain some fuel so a pour has room (server), record the level, and check
    /// it rose since (any peer: the level is the replicated Attr_Fuel attribute, but the record is per peer).
    /// </summary>
    public sealed class FuelScenarioLibrary : IScenarioLibrary
    {
        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private float? _baseline;

        public FuelScenarioLibrary(IActorRegistry actors, INetworkService network)
        {
            _actors = actors;
            _network = network;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("DrainTank", Drain),
            new ScenarioCommand("RecordTankLevel", _ => Record())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("TankLevelRose", _ => Rose())
        };

        private ScenarioCheck Drain(string litres)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var tank = Tank();
            if (tank == null) return ScenarioCheck.Fail("no fuel tank on any airship");

            tank.Consume(float.Parse(litres, CultureInfo.InvariantCulture));
            return ScenarioCheck.Pass($"tank at {tank.Level:0.0} of {tank.Capacity:0}");
        }

        private ScenarioCheck Record()
        {
            var tank = Tank();
            if (tank == null) return ScenarioCheck.Fail("no fuel tank on any airship");

            _baseline = tank.Level;
            return ScenarioCheck.Pass($"tank at {tank.Level:0.0}");
        }

        private ScenarioCheck Rose()
        {
            var tank = Tank();
            if (tank == null) return ScenarioCheck.Fail("no fuel tank on any airship");
            if (_baseline == null) return ScenarioCheck.Fail("RecordTankLevel has not run");

            string detail = $"tank {_baseline:0.0} -> {tank.Level:0.0}";
            return tank.Level > _baseline + 1f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private IFuelTank? Tank() =>
            _actors.GetActors<IAirshipView>().Select(FuelTankLocator.Find).FirstOrDefault(tank => tank != null);
    }
}
