#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Hud;
using TinCan.Core.Domain.Networking;
using TinCan.Core.UI;
using TinCan.Core.Ship;
using UnityEngine;
using TinCan.Features.Respawn;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for falling overboard. Commands: send the subject overboard through the same handler the cloud
    /// boundary calls (server; scripted runs switch the cloud reset off), and pick the respawn row in the subject's own
    /// menu (subject peer: the real menu command and its request). Probes: the subject waits overboard (server), this
    /// peer shows the overboard menu, and the HUD's Respawn line.
    /// </summary>
    public sealed class RespawnScenarioLibrary : IScenarioLibrary
    {
        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;
        private readonly OverboardUseCase _overboard;
        private readonly IMenuSystem _menus;
        private readonly IHudValues _hud;
        private readonly IActorRegistry _actors;
        private readonly LeashStore _leashes;
        private readonly LeashRopePresenter _ropes;

        public RespawnScenarioLibrary(ScenarioSubject subject, INetworkService network, OverboardUseCase overboard, IMenuSystem menus, IHudValues hud, IActorRegistry actors,
            LeashStore leashes, LeashRopePresenter ropes)
        {
            _subject = subject;
            _network = network;
            _overboard = overboard;
            _menus = menus;
            _hud = hud;
            _actors = actors;
            _leashes = leashes;
            _ropes = ropes;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("SendSubjectOverboard", _ => SendOverboard()),
            new ScenarioCommand("ChooseRespawn", _ => Choose(OverboardMenus.RespawnItem)),
            new ScenarioCommand("ChooseLeash", _ => Choose(OverboardMenus.LeashItem)),
            new ScenarioCommand("CrankWinch", _ => CrankWinch())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectOverboard", _ => CheckOverboard(expected: true)),
            new ScenarioProbe("SubjectNotOverboard", _ => CheckOverboard(expected: false)),
            new ScenarioProbe("SubjectAboard", _ => CheckAboard()),
            new ScenarioProbe("SubjectLeashed", _ => CheckLeashed()),
            new ScenarioProbe("LeashRopeDrawn", _ => CheckRope()),
            new ScenarioProbe("OverboardMenuOpen", _ => _menus.Current?.MenuId == OverboardMenuUseCase.MenuId
                ? ScenarioCheck.Pass("overboard menu open")
                : ScenarioCheck.Fail($"menu {_menus.Current?.MenuId ?? "none"}")),
            new ScenarioProbe("RespawnHudShows", expected => _hud.All.TryGetValue(OverboardMenuUseCase.HudKey, out var text) && text == expected
                ? ScenarioCheck.Pass($"HUD: {text}")
                : ScenarioCheck.Fail($"HUD: {(_hud.All.TryGetValue(OverboardMenuUseCase.HudKey, out var shown) ? shown : "none")}"))
        };

        private ScenarioCheck SendOverboard()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");

            return _overboard.TryHandleOverboard(subject)
                ? ScenarioCheck.Pass("subject overboard")
                : ScenarioCheck.Fail("the overboard handler declined (no ship, or no effect in the rules)");
        }

        /// <summary>Subject peer: picks a row of the open overboard menu, through the real menu command and its request.</summary>
        private ScenarioCheck Choose(string item)
        {
            if (_menus.Current?.MenuId != OverboardMenuUseCase.MenuId) return ScenarioCheck.Fail("the overboard menu is not open on this peer");
            if (_menus.Current.Items.All(row => row.ItemId != item)) return ScenarioCheck.Fail($"the overboard menu has no '{item}' row");
            _menus.Invoke(item);
            return ScenarioCheck.Pass($"chose {item}");
        }

        /// <summary>Server: cranks the winch on the ship's leash point, as Interact on it would.</summary>
        private ScenarioCheck CrankWinch()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var point = _actors.GetActors<ILeashPoint>().FirstOrDefault(candidate => candidate.IsSimulating);
            if (point is not Component component || component.GetComponent<ILeashWinch>() == null) return ScenarioCheck.Fail("no leash point with a winch");
            if (!_leashes.AnyOn(point)) return ScenarioCheck.Fail("nobody hangs on the leash");

            bool reeling = _leashes.ToggleWinch(point);
            return reeling ? ScenarioCheck.Pass("the winch reels in") : ScenarioCheck.Fail("the winch stopped (it was already reeling)");
        }

        private ScenarioCheck CheckLeashed()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");

            bool leashed = _overboard.IsLeashed(subject.Id);
            string length = _leashes.TryGet(subject.Id, out var leash) ? $", rope {leash.Length:0.0} m" : string.Empty;
            return leashed ? ScenarioCheck.Pass($"leashed{length}") : ScenarioCheck.Fail("not leashed");
        }

        /// <summary>This peer draws the subject's rope (the host draws a client's from the replicated leashed tag).</summary>
        private ScenarioCheck CheckRope()
        {
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");
            var rope = _ropes.RopeOf(subject.Id);
            return rope != null ? ScenarioCheck.Pass($"rope from {rope.GetPosition(0)} to {rope.GetPosition(1)}") : ScenarioCheck.Fail("no rope drawn");
        }

        /// <summary>
        /// The subject stands aboard on this peer: grounded, within the ship's footprint (its colliders' bounds, in ship space)
        /// and no higher than a few metres over it. Standing on a crewmate's head on deck counts: both spawn at the same point.
        /// </summary>
        private ScenarioCheck CheckAboard()
        {
            var movement = _subject.Resolve()?.Movement;
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.Transform != null);
            if (movement == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");

            var colliders = ship.Transform.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).ToArray();
            if (colliders.Length == 0) return ScenarioCheck.Fail("the ship has no solid colliders");
            var bounds = colliders[0].bounds;
            foreach (var collider in colliders) bounds.Encapsulate(collider.bounds);

            Vector3 position = movement.Transform.position;
            bool over = position.x >= bounds.min.x && position.x <= bounds.max.x && position.z >= bounds.min.z && position.z <= bounds.max.z;
            bool aboard = movement.CurrentGround.IsGrounded && over && position.y >= bounds.min.y && position.y <= bounds.max.y + MaxAboveShip;
            string detail = $"{(aboard ? "aboard" : "not aboard")} at {ship.Transform.InverseTransformPoint(position)} in ship space, grounded {movement.CurrentGround.IsGrounded}";
            return aboard ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private const float MaxAboveShip = 3f;

        private ScenarioCheck CheckOverboard(bool expected)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");

            bool overboard = _overboard.IsOverboard(subject.Id);
            string detail = overboard ? "overboard" : "aboard";
            return overboard == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }
    }
}
