#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Features.CloudFlight;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the cloud flight (.docs/plans/cloud-sea.md, "Soaring and cloud shelves"). Command: PitchShip
    /// (server: sets the first ship's nose to a pitch in degrees, up above zero, keeping its heading; nobody is at the
    /// helm, so the forces alone fly it). Probes, on any peer (the ship's pose replicates): ShipAtRest (the ship floats at
    /// its rest height on the cloud, within a few metres) and ShipAboveCloud (it is at least this many metres above it).
    /// </summary>
    public sealed class CloudFlightScenarioLibrary : IScenarioLibrary
    {
        private const float RestTolerance = 4f;

        private readonly CloudFlightUseCase _flight;
        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;

        public CloudFlightScenarioLibrary(CloudFlightUseCase flight, IActorRegistry actors, INetworkService network)
        {
            _flight = flight;
            _actors = actors;
            _network = network;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PitchShip", Pitch),
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("ShipAtRest", _ => CheckAtRest()),
            new ScenarioProbe("ShipAboveCloud", CheckAbove),
        };

        private ScenarioCheck Pitch(string degrees)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var ship = Ship();
            if (ship == null) return ScenarioCheck.Fail("no ship");
            if (!float.TryParse(degrees, NumberStyles.Float, CultureInfo.InvariantCulture, out float up)) return ScenarioCheck.Fail($"not a pitch: {degrees}");

            float heading = ship.Transform.eulerAngles.y;
            ship.Transform.rotation = Quaternion.Euler(-up, heading, 0f);
            return ScenarioCheck.Pass($"nose at {up:0} deg, {Height(ship):0.0} m from rest");
        }

        private ScenarioCheck CheckAtRest()
        {
            var ship = Ship();
            if (ship == null) return ScenarioCheck.Fail("no ship");

            float height = Height(ship);
            string detail = $"{height:0.0} m from its rest height";
            return Mathf.Abs(height) <= RestTolerance ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckAbove(string metres)
        {
            var ship = Ship();
            if (ship == null) return ScenarioCheck.Fail("no ship");
            if (!float.TryParse(metres, NumberStyles.Float, CultureInfo.InvariantCulture, out float least)) return ScenarioCheck.Fail($"not metres: {metres}");

            float height = Height(ship);
            string detail = $"{height:0.0} m above its rest height";
            return height >= least ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private float Height(IAirshipView ship) => _flight.HeightAboveRest(ship);

        private IAirshipView? Ship() =>
            _actors.GetActors<IAirshipView>().FirstOrDefault(airship => airship.Transform != null);
    }
}
