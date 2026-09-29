#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Airship;
using TinCan.Features.Airship.Damage;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Targeting;
using UnityEngine;
using VContainer;
using TinCan.Core.Domain.Hud;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for ship damage. Commands (server) break or restore a part by index. Probes read this peer's
    /// view: part health and marker (replicated), the ship's tags and leak rate (replicated attributes), the tank level,
    /// and the local HUD line.
    /// </summary>
    public sealed class ShipDamageScenarioLibrary : IScenarioLibrary
    {
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IShipBreakage _breakage;
        private readonly IHudValues _hud;
        private readonly IGameplayTagRegistry? _tags;
        private readonly ScenarioSubject _subject;
        private readonly IHumanoidRespawnService _respawn;
        private readonly ShipDamageConfig _config;
        private readonly ITargetingService _targeting;

        // Where PlaceSubjectAtPoint stands the subject: this far behind the part along the way the subject already faces
        // (facing follows the look input every tick, so a teleport cannot turn a player), standing on the deck below.
        private const float StandOff = 1.5f;

        private float? _fuelBaseline;

        public ShipDamageScenarioLibrary(INetworkService network, IActorRegistry actors, IShipBreakage breakage, IHudValues hud,
            ScenarioSubject subject, IHumanoidRespawnService respawn, ShipDamageConfig config, ITargetingService targeting, IObjectResolver resolver)
        {
            _network = network;
            _actors = actors;
            _breakage = breakage;
            _hud = hud;
            _subject = subject;
            _respawn = respawn;
            _config = config;
            _targeting = targeting;
            _tags = resolver.TryResolve<IGameplayTagRegistry>(out var tags) ? tags : null;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("BreakPoint", index => ServerOnly(() => _breakage.TryBreak(int.Parse(index)), $"part {index} broken")),
            new ScenarioCommand("RestorePoint", index => ServerOnly(() => _breakage.TryRestore(int.Parse(index)), $"part {index} restored")),
            new ScenarioCommand("RecordFuel", _ => RecordFuel()),
            new ScenarioCommand("PlaceSubjectAtPoint", PlaceSubject)
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectFacesPoint", FacesPoint),
            new ScenarioProbe("PointBroken", index => CheckPoint(index, broken: true)),
            new ScenarioProbe("PointHealthy", index => CheckPoint(index, broken: false)),
            new ScenarioProbe("MarkerShown", index => CheckMarker(index, shown: true)),
            new ScenarioProbe("MarkerHidden", index => CheckMarker(index, shown: false)),
            new ScenarioProbe("PointHasTag", arg => CheckPointTag(arg, expected: true)),
            new ScenarioProbe("PointLacksTag", arg => CheckPointTag(arg, expected: false)),
            new ScenarioProbe("ShipHasTag", tag => CheckShipTag(tag, expected: true)),
            new ScenarioProbe("ShipLacksTag", tag => CheckShipTag(tag, expected: false)),
            new ScenarioProbe("LeakRateAbove", threshold => CheckLeak(threshold, above: true)),
            new ScenarioProbe("LeakRateAtMost", threshold => CheckLeak(threshold, above: false)),
            new ScenarioProbe("FuelDroppedBy", amount => CheckFuelDrop(amount)),
            new ScenarioProbe("HudShows", key => _hud.All.TryGetValue(key, out var value)
                ? ScenarioCheck.Pass($"{key}: {value}")
                : ScenarioCheck.Fail($"no '{key}' on the HUD")),
            new ScenarioProbe("HudHidden", key => _hud.All.TryGetValue(key, out var value)
                ? ScenarioCheck.Fail($"{key}: {value}")
                : ScenarioCheck.Pass($"no '{key}' on the HUD"))
        };

        /// <summary>
        /// Server: teleports the subject in front of the part, through the respawn service (the owner snaps). It keeps the
        /// subject's current facing, because a player's facing follows its look input every tick.
        /// </summary>
        private ScenarioCheck PlaceSubject(string index)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var point = Point(index)?.Transform;
            var ship = Ship()?.Transform;
            if (subject == null || point == null || ship == null) return ScenarioCheck.Fail("no subject, part or ship");

            var body = subject.Movement.Transform;
            Vector3 facing = Vector3.ProjectOnPlane(body.forward, ship.up).normalized;
            Vector3 stand = point.position - facing * StandOff;
            stand = ScenarioPlacement.OnGround(body, stand, ship.up);
            _respawn.ResetCharacter(subject, stand, body.rotation);
            return ScenarioCheck.Pass($"subject placed {StandOff} m behind part {index}");
        }

        /// <summary>
        /// Would the repair use case target this part from where the subject stands on this peer? Asks the targeting service
        /// with the repair ability's definition, so the scenario and the game share one answer.
        /// </summary>
        private ScenarioCheck FacesPoint(string index)
        {
            var subject = _subject.Resolve();
            var point = Point(index);
            var definition = _config.RepairAbility?.Targeting;
            if (subject == null || point == null) return ScenarioCheck.Fail("no subject or part");
            if (definition == null) return ScenarioCheck.Fail("ShipDamageConfig.RepairAbility has no TargetingDefinition");

            if (!_targeting.TryAcquire(new HumanoidTargeter(subject), definition, out var result))
                return ScenarioCheck.Fail($"{definition.name} acquires nothing from here");
            if (result.Target is not IShipDamagePoint targeted || targeted.Index != point.Index)
                return ScenarioCheck.Fail($"{definition.name} picks something else ({result.Target})");

            return ScenarioCheck.Pass($"{definition.name}: part {index} at {result.Distance:0.00} m, {result.HorizontalAngle:0} deg");
        }

        private ScenarioCheck ServerOnly(Func<bool> action, string success)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            return action() ? ScenarioCheck.Pass(success) : ScenarioCheck.Fail("no such part, or no ship with damage points");
        }

        private ScenarioCheck CheckPoint(string index, bool broken)
        {
            var point = Point(index);
            if (point == null) return ScenarioCheck.Fail($"no damage point {index}");

            string detail = $"part {index} health {point.Health01:0.00}";
            return point.IsBroken == broken ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckMarker(string index, bool shown)
        {
            var marker = Point(index)?.Transform?.FindDescendant("Marker");
            if (marker == null) return ScenarioCheck.Fail($"no Marker under damage point {index}");

            bool active = marker.gameObject.activeSelf;
            string detail = $"marker {index} {(active ? "shown" : "hidden")}";
            return active == shown ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckShipTag(string tagName, bool expected)
        {
            var tag = ScenarioTags.Find(tagName, _tags);
            if (tag == null) return ScenarioCheck.Fail($"no GameplayTag named '{tagName}'");
            var controller = (Ship() as IShipState)?.Controller;
            if (controller == null) return ScenarioCheck.Fail("no ship ability controller");

            bool has = controller.HasTag(tag);
            string detail = $"ship {(has ? "has" : "lacks")} {tagName}";
            return has == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Argument "index:Tag.Name", e.g. "0:State.Damaged".</summary>
        private ScenarioCheck CheckPointTag(string argument, bool expected)
        {
            var parts = argument.Split(':');
            if (parts.Length != 2) return ScenarioCheck.Fail($"expected 'index:Tag', got '{argument}'");
            var controller = Point(parts[0])?.Controller;
            if (controller == null) return ScenarioCheck.Fail($"damage point {parts[0]} has no ability controller");
            var tag = ScenarioTags.Find(parts[1], _tags);
            if (tag == null) return ScenarioCheck.Fail($"no GameplayTag named '{parts[1]}'");

            bool has = controller.HasTag(tag);
            string detail = $"part {parts[0]} {(has ? "has" : "lacks")} {parts[1]}";
            return has == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckLeak(string threshold, bool above)
        {
            var ship = Ship();
            var attribute = Tank()?.Config?.LeakRateAttribute;
            var controller = (ship as IShipState)?.Controller;
            if (attribute == null || controller == null) return ScenarioCheck.Fail("no leak-rate attribute (FuelConfig.LeakRateAttribute) or ship controller");
            if (!controller.TryGetAttribute(attribute, out var rate)) return ScenarioCheck.Fail("ship has no leak-rate attribute value");

            float limit = float.Parse(threshold, System.Globalization.CultureInfo.InvariantCulture);
            bool ok = above ? rate.CurrentValue > limit : rate.CurrentValue <= limit;
            string detail = $"leak {rate.CurrentValue:0.###} L/s";
            return ok ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck RecordFuel()
        {
            var tank = Tank();
            if (tank == null) return ScenarioCheck.Fail("no fuel tank");
            _fuelBaseline = tank.Level;
            return ScenarioCheck.Pass($"fuel {tank.Level:0.00}");
        }

        private ScenarioCheck CheckFuelDrop(string amount)
        {
            var tank = Tank();
            if (tank == null) return ScenarioCheck.Fail("no fuel tank");
            if (_fuelBaseline == null) return ScenarioCheck.Fail("RecordFuel has not run");

            float drop = _fuelBaseline.Value - tank.Level;
            float wanted = float.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
            string detail = $"fuel {_fuelBaseline:0.00} -> {tank.Level:0.00} (dropped {drop:0.00})";
            return drop >= wanted ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private IAirshipView? Ship() => _actors.GetActors<IAirshipView>().FirstOrDefault(ship => ShipDamageLocator.FindPoints(ship).Count > 0);

        private IFuelTank? Tank() => Ship() is { } ship ? FuelTankLocator.Find(ship) : null;

        private IShipDamagePoint? Point(string index) =>
            Ship() is { } ship && int.TryParse(index, out int i)
                ? ShipDamageLocator.FindPoints(ship).FirstOrDefault(point => point.Index == i)
                : null;
    }
}
