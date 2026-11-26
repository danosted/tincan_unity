#nullable enable
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Features.Encounters;
using TinCan.Features.SkyHazards;
using TinCan.Features.Storm;
using TinCan.Features.Weapons.Cannon;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the storm (plan: storm-crossing.md, ST2 to ST5).
    /// <list type="bullet">
    /// <item>Probes on any peer with a view: the storm's weight on the light and air there (depth into the zone times
    /// strength), and whether the shaders have its region; the heart's replicated strength; whether this peer draws a
    /// target with some seconds left on it; the gust blowing (the server's push, or the gust where another peer's view
    /// is).</item>
    /// <item>Server probes: debris launched, and hazard hits on the ship.</item>
    /// <item>Server: throw one piece of debris dead at the ship.</item>
    /// <item>Server: put the zone's centre (the heart, at severity 1) this far out from a cannon along its rest direction,
    /// so the ship is in the eye and the targets are in the cannon's reach.</item>
    /// <item>Subject peer: point the manned cannon at the target this peer draws, as the mouse would.</item>
    /// </list>
    /// </summary>
    public sealed class StormScenarioLibrary : IScenarioLibrary
    {
        private readonly StormWeatherPresenter _weather;
        private readonly IStormIntensity _intensity;
        private readonly StormHeartPresenter _heart;
        private readonly StormConfig _config;
        private readonly EncounterZoneConfig _zoneConfig;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly GunnerAimUseCase _gunner;
        private readonly StormGustUseCase _gusts;
        private readonly StormDebrisUseCase _debris;
        private readonly ISkyHazards _hazards;

        public StormScenarioLibrary(StormWeatherPresenter weather, IStormIntensity intensity, StormHeartPresenter heart, StormConfig config,
            EncounterZoneConfig zoneConfig, INetworkService network, IActorRegistry actors, ITimeService time, GunnerAimUseCase gunner,
            StormGustUseCase gusts, StormDebrisUseCase debris, ISkyHazards hazards)
        {
            _gusts = gusts;
            _debris = debris;
            _hazards = hazards;
            _weather = weather;
            _intensity = intensity;
            _heart = heart;
            _config = config;
            _zoneConfig = zoneConfig;
            _network = network;
            _actors = actors;
            _time = time;
            _gunner = gunner;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlaceHeartOffCannon", PlaceHeartOffCannon),
            new ScenarioCommand("AimCannonAtStormTarget", AimAtTarget),
            new ScenarioCommand("ThrowStormDebrisAtShip", ThrowAtShip)
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("StormWeightAbove", at => Compare(at, above: true)),
            new ScenarioProbe("StormWeightBelow", at => Compare(at, above: false)),
            new ScenarioProbe("StormRegionSet", _ => _intensity.Region is { } region
                ? ScenarioCheck.Pass($"the storm stands at {region.Centre}, {region.Radius:0} m round")
                : ScenarioCheck.Fail("no storm region")),
            new ScenarioProbe("HeartStrengthBelow", CheckHeartStrength),
            new ScenarioProbe("StormTargetUpFor", CheckTargetUp),
            new ScenarioProbe("StormGustAbove", CheckGust),
            new ScenarioProbe("StormDebrisLaunched", count => _debris.Launched >= Parse(count)
                ? ScenarioCheck.Pass($"{_debris.Launched} pieces of debris on the wind")
                : ScenarioCheck.Fail($"{_debris.Launched} pieces of debris on the wind (server-only probe)")),
            new ScenarioProbe("StormDebrisHits", count => _hazards.Hits >= Parse(count)
                ? ScenarioCheck.Pass($"{_hazards.Hits} hazard hits on the ship")
                : ScenarioCheck.Fail($"{_hazards.Hits} hazard hits on the ship (server-only probe)"))
        };

        /// <summary>
        /// A gust of at least this many m/s: on the server the push the ship got; on another peer the gust where its view
        /// is (the same seed and clock, so the wind it hears swell matches the push).
        /// </summary>
        private ScenarioCheck CheckGust(string speed)
        {
            float gust;
            if (_network.IsServer) gust = _gusts.LastGust.magnitude;
            else
            {
                if (_intensity.Region is not { } region || _intensity.Camera is not { } camera) return ScenarioCheck.Fail("no storm region or no view");
                float fury = StormWindProcessor.Fury(_intensity.Depth, _intensity.Strength);
                gust = StormWindProcessor.Gust(region.Centre, camera, fury, _zoneConfig.Severity, _config.Seed, _time.Time, _config).magnitude;
            }
            string detail = $"gust {gust:0.0} m/s on this peer";
            return gust >= Parse(speed) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Server: "metres": one piece of debris dead at the ship from this far upwind.</summary>
        private ScenarioCheck ThrowAtShip(string metres)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(s => s.IsSimulating && s.Transform != null);
            if (ship == null) return ScenarioCheck.Fail("no ship");
            return _debris.ThrowAt(ship, Parse(metres))
                ? ScenarioCheck.Pass($"debris thrown at the ship from {metres} m upwind")
                : ScenarioCheck.Fail("the ship is not in a raging storm");
        }

        private ScenarioCheck Compare(string threshold, bool above)
        {
            float limit = Parse(threshold);
            string detail = $"storm weight {_weather.Weight:0.00} (depth {_intensity.Depth:0.00}, strength {_intensity.Strength:0.00})";
            bool ok = above ? _weather.Weight > limit : _weather.Weight < limit;
            return ok ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckHeartStrength(string threshold)
        {
            if (Heart() is not { } heart) return ScenarioCheck.Fail("no storm heart");
            string detail = $"heart strength {heart.Strength:0.00}";
            return heart.Strength < Parse(threshold) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>This peer draws a target, and it stays up at least this many more seconds (time to aim and charge).</summary>
        private ScenarioCheck CheckTargetUp(string seconds)
        {
            if (_heart.TargetShown is not { } target) return ScenarioCheck.Fail("no target drawn");
            float left = StormHeartProcessor.TargetSecondsLeft(_time.Time, _zoneConfig.Severity, _config);
            string detail = $"target at {target}, up {left:0.0} s more";
            return left >= Parse(seconds) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>"metres [cannon] [degrees]": out from the cannon, turned this far right of its rest direction.</summary>
        private ScenarioCheck PlaceHeartOffCannon(string arguments)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var parts = arguments.Split(' ');
            var cannon = CannonScenarioLibrary.FindCannon(_actors, parts.Length > 1 ? parts[1] : "");
            var zone = _actors.GetActors<IEncounterZone>().FirstOrDefault(candidate => candidate.IsSimulating);
            if (cannon?.Base == null || zone == null) return ScenarioCheck.Fail("no cannon or no zone");

            float turn = parts.Length > 2 ? Parse(parts[2]) : 0f;
            Vector3 rest = Quaternion.AngleAxis(turn, Vector3.up) * cannon.Base.forward;
            var flat = new Vector2(rest.x, rest.z).normalized;
            var at = new Vector2(cannon.Base.position.x, cannon.Base.position.z) + flat * Parse(parts[0]);
            zone.ServerSetCentre(at);
            return ScenarioCheck.Pass($"the storm's heart {parts[0]} m off the cannon, at {at}");
        }

        private ScenarioCheck AimAtTarget(string _)
        {
            if (_gunner.LocalAim is not { } aim) return ScenarioCheck.Fail("subject-peer command: this peer does not man a cannon");
            if (_heart.TargetShown is not { } target) return ScenarioCheck.Fail("no target drawn");
            var cannon = _actors.GetActors<ICannon>().FirstOrDefault(candidate => candidate.Id == aim.Cannon);
            var pivot = cannon?.PitchPivot ?? cannon?.Base;
            if (cannon?.Base == null || pivot == null) return ScenarioCheck.Fail("the manned cannon is gone");

            Vector3 local = Quaternion.Inverse(cannon.Base.rotation) * (target - pivot.position);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float elevation = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            if (!_gunner.TrySetAim(new Vector2(yaw, elevation))) return ScenarioCheck.Fail("could not set the aim");
            return ScenarioCheck.Pass($"barrel at yaw {yaw:0.#}, elevation {elevation:0.#} for the target {local.magnitude:0} m off");
        }

        private IStormHeart? Heart() =>
            _actors.GetActors<IEncounterZone>().Select(StormHearts.Of).FirstOrDefault(heart => heart != null);

        private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }
}
