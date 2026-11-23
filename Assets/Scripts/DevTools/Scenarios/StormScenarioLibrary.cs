#nullable enable
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Encounters;
using TinCan.Features.Storm;
using TinCan.Features.Weapons.Cannon;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the storm (plan: storm-crossing.md, ST2 and ST5).
    /// <list type="bullet">
    /// <item>Probes on any peer with a view: the storm's weight on the light and air there (depth into the zone times
    /// strength), and whether the shaders have its region; the heart's replicated strength; whether this peer draws a
    /// target with some seconds left on it.</item>
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

        public StormScenarioLibrary(StormWeatherPresenter weather, IStormIntensity intensity, StormHeartPresenter heart, StormConfig config,
            EncounterZoneConfig zoneConfig, INetworkService network, IActorRegistry actors, ITimeService time, GunnerAimUseCase gunner)
        {
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
            new ScenarioCommand("AimCannonAtStormTarget", AimAtTarget)
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("StormWeightAbove", at => Compare(at, above: true)),
            new ScenarioProbe("StormWeightBelow", at => Compare(at, above: false)),
            new ScenarioProbe("StormRegionSet", _ => _intensity.Region is { } region
                ? ScenarioCheck.Pass($"the storm stands at {region.Centre}, {region.Radius:0} m round")
                : ScenarioCheck.Fail("no storm region")),
            new ScenarioProbe("HeartStrengthBelow", CheckHeartStrength),
            new ScenarioProbe("StormTargetUpFor", CheckTargetUp)
        };

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
