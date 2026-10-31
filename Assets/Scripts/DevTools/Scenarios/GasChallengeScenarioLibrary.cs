#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Features.Airship.Damage;
using TinCan.Features.GasChallenge;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for gas fields (plan: gas-squall-event.md). Commands (server) put a pocket of the field onto the
    /// ship, so the real touch rule sets it off, disperse the rest without a blast, and restore every broken part. Probes
    /// read this peer's replicated view: live and detonated pockets, and broken points beside fittings.
    /// </summary>
    public sealed class GasChallengeScenarioLibrary : IScenarioLibrary
    {
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IGasField _field;
        private readonly IShipBreakage _breakage;

        public GasChallengeScenarioLibrary(INetworkService network, IActorRegistry actors, IGasField field, IShipBreakage breakage)
        {
            _network = network;
            _actors = actors;
            _field = field;
            _breakage = breakage;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("MoveGasPocketOntoShip", _ => MovePocketOntoShip()),
            new ScenarioCommand("DisperseGasField", _ => Disperse()),
            new ScenarioCommand("RestoreAllPoints", _ => RestoreAll())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("GasPocketsLive", atLeast => Count(pocket => !pocket.Detonated, atLeast, "live gas pockets")),
            new ScenarioProbe("GasPocketsDetonated", atLeast => Count(pocket => pocket.Detonated, atLeast, "detonated gas pockets")),
            new ScenarioProbe("BrokenFittingPoints", CheckBrokenFittings)
        };

        /// <summary>Server: the field's nearest live pocket jumps onto the ship; the touch rule does the rest.</summary>
        private ScenarioCheck MovePocketOntoShip()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var ship = Ship();
            if (ship == null) return ScenarioCheck.Fail("no ship");

            var at = ship.Transform.position;
            var pocket = _field.Pockets.Where(candidate => !candidate.Detonated)
                .OrderBy(candidate => (candidate.Center - at).sqrMagnitude)
                .FirstOrDefault();
            if (pocket == null) return ScenarioCheck.Fail("no live pocket in the field");

            pocket.Volume.transform.position = at;
            return ScenarioCheck.Pass($"a pocket moved onto the ship at {at}");
        }

        /// <summary>Server: every pocket of the field counts as gone off (no blast), so the field is cleared.</summary>
        private ScenarioCheck Disperse()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            foreach (var pocket in _field.Pockets) pocket.Volume.MarkDetonated();
            return ScenarioCheck.Pass($"{_field.Pockets.Count} pocket(s) dispersed");
        }

        private ScenarioCheck RestoreAll()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var ship = Ship();
            if (ship == null) return ScenarioCheck.Fail("no ship");

            var broken = ShipDamageLocator.FindPoints(ship).Where(point => point.Controller.IsDamaged()).Select(point => point.Index).ToList();
            foreach (int index in broken) _breakage.TryRestore(index);
            return ScenarioCheck.Pass($"restored {broken.Count} part(s)");
        }

        private ScenarioCheck Count(System.Func<IGasPocket, bool> which, string atLeast, string what)
        {
            int count = _actors.GetActors<IGasPocket>().Count(pocket => pocket.IsSimulating && which(pocket));
            string detail = $"{count} {what} on this peer";
            return count >= int.Parse(atLeast, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckBrokenFittings(string atLeast)
        {
            var ship = Ship();
            if (ship == null) return ScenarioCheck.Fail("no ship");

            var points = ShipDamageLocator.FindPoints(ship);
            var broken = points.Where(point => point.Socket != null && point.Controller.IsDamaged()).ToList();
            string detail = $"{broken.Count} broken beside fittings ({points.Count(point => point.Socket != null)} fitting points, " +
                            $"{points.Count} in all): {string.Join(", ", broken.Select(point => $"#{point.Index} {point.Socket}"))}";
            return broken.Count >= int.Parse(atLeast, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        // Not IsSimulating: a client does not simulate the ship, but sees it and its points.
        private IAirshipView? Ship() =>
            _actors.GetActors<IAirshipView>().FirstOrDefault(ship => ship.Transform != null && ShipDamageLocator.FindPoints(ship).Count > 0);
    }
}
