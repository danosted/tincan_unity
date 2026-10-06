#nullable enable
using System;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Sockets;
using TinCan.Core.UI;
using TinCan.Features.ShipSockets;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for ship sockets. Commands: stand the subject on the deck just aft of the ship's first socket
    /// (server); pick a fitting in the open fitting menu (subject peer). Probes: the fitting menu is open on this peer; a
    /// fitting is mounted (replicated) and its object stands on its socket on this peer. Plan:
    /// .docs/plans/modular-airship-builder.md (S5).
    /// </summary>
    public sealed class ShipSocketsScenarioLibrary : IScenarioLibrary
    {
        private const float StandAft = 1.3f;

        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IHumanoidRespawnService _respawn;
        private readonly IShipSockets _sockets;
        private readonly IShipFittingCatalog _catalog;
        private readonly IMenuSystem _menus;

        public ShipSocketsScenarioLibrary(ScenarioSubject subject, INetworkService network, IActorRegistry actors,
            IHumanoidRespawnService respawn, IShipSockets sockets, IShipFittingCatalog catalog, IMenuSystem menus)
        {
            _subject = subject;
            _network = network;
            _actors = actors;
            _respawn = respawn;
            _sockets = sockets;
            _catalog = catalog;
            _menus = menus;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlaceSubjectAtSocket", _ => PlaceAtSocket()),
            new ScenarioCommand("ChooseFitting", Choose),
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("FittingMenuOpen", _ => _menus.Current?.MenuId == ShipFittingMenuUseCase.MenuId
                ? ScenarioCheck.Pass("the fitting menu is open")
                : ScenarioCheck.Fail($"menu: {_menus.Current?.MenuId ?? "none"}")),
            new ScenarioProbe("SocketFitted", CheckFitted),
        };

        private ScenarioCheck PlaceAtSocket()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var ship = Ship();
            var socket = ship == null ? null : _sockets.SocketsOf(ship.Id).Select(s => s.Mount).FirstOrDefault(m => m != null);
            if (subject == null || body == null || ship == null || socket == null) return ScenarioCheck.Fail("no subject, ship or socket");

            var stand = ScenarioPlacement.OnGround(body, socket.position - ship.Transform.forward * StandAft, ship.Transform.up);
            _respawn.ResetCharacter(subject, stand, ship.Transform.rotation);
            return ScenarioCheck.Pass($"subject placed {StandAft} m aft of {socket.name}");
        }

        private ScenarioCheck Choose(string fittingId)
        {
            if (_menus.Current?.MenuId != ShipFittingMenuUseCase.MenuId) return ScenarioCheck.Fail("the fitting menu is not open");
            _menus.Invoke(fittingId);
            return ScenarioCheck.Pass($"chose {fittingId}");
        }

        /// <summary>This peer: the fitting is in the replicated list, and its object stands on its socket.</summary>
        private ScenarioCheck CheckFitted(string fittingId)
        {
            var ship = Ship();
            var state = _actors.GetActors<IShipFittingState>().FirstOrDefault(s => s.Ship != null);
            if (ship == null || state == null) return ScenarioCheck.Fail("no ship or no sockets state");

            var mounted = state.Mounted.Where(m => m.FittingId.ToString() == fittingId).ToList();
            if (mounted.Count == 0) return ScenarioCheck.Fail($"{fittingId} is not mounted (mounted: {state.Mounted.Count})");
            if (!_catalog.TryGet(fittingId, out var fitting) || fitting.Prefab == null) return ScenarioCheck.Fail($"no fitting {fittingId}");

            var socket = _sockets.SocketsOf(ship.Id).FirstOrDefault(s => s.Id.Equals(mounted[0].Socket));
            if (socket.Mount == null) return ScenarioCheck.Fail($"the ship has no {mounted[0].Socket} on this peer");

            var standing = ship.Transform.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith(fitting.Prefab.name, StringComparison.Ordinal))
                .Select(t => Vector3.Distance(t.position, socket.Mount.position))
                .DefaultIfEmpty(float.PositiveInfinity)
                .Min();
            string detail = $"{fittingId} in {mounted[0].Socket}, its object {standing:0.00} m from the socket";
            return standing < 0.5f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private IAirshipView? Ship() => _actors.GetActors<IAirshipView>().FirstOrDefault();
    }
}
