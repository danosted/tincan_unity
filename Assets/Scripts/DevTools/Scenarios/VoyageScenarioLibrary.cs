#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Core.UI;
using TinCan.Features.Voyage;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the voyage session.
    /// <list type="bullet">
    /// <item>Server commands begin a voyage, cast off, put the destination on the ship (arrive) or sink it.</item>
    /// <item><c>PressMenuItem</c> runs on any peer: it invokes a row of the menu open there, as a click on it would.</item>
    /// <item>Probes read the replicated voyage state and this peer's menu, so they answer on host and client alike.</item>
    /// </list>
    /// </summary>
    public sealed class VoyageScenarioLibrary : IScenarioLibrary
    {
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IVoyage _voyage;
        private readonly IMenuSystem _menus;
        private int _recordedVoyage;

        public VoyageScenarioLibrary(INetworkService network, IActorRegistry actors, IVoyage voyage, IMenuSystem menus)
        {
            _network = network;
            _actors = actors;
            _voyage = voyage;
            _menus = menus;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("VoyageBegin", _ => ServerOnly(_voyage.Begin, "voyage begun")),
            new ScenarioCommand("VoyageCastOff", _ => ServerOnly(_voyage.CastOff, "cast off")),
            new ScenarioCommand("VoyageArriveNow", _ => ServerOnly(ArriveNow, "destination moved onto the ship")),
            new ScenarioCommand("SinkShip", _ => ServerOnly(Sink, "ship health set to 0")),
            new ScenarioCommand("PressMenuItem", Press),
            new ScenarioCommand("RecordVoyage", _ => Record())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("VoyagePhase", CheckPhase),
            new ScenarioProbe("VoyageAdvanced", _ => CheckAdvanced()),
            new ScenarioProbe("MenuShown", CheckMenu),
            new ScenarioProbe("NoMenuShown", _ => _menus.IsOpen
                ? ScenarioCheck.Fail($"menu '{_menus.Current?.MenuId}' is open")
                : ScenarioCheck.Pass("no menu open")),
            new ScenarioProbe("ShipHealthFull", _ => CheckHealthFull())
        };

        private ScenarioCheck ServerOnly(Func<bool> action, string success)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            return action() ? ScenarioCheck.Pass(success) : ScenarioCheck.Fail("no ship or no voyage state (is the Voyage installer in the profile?)");
        }

        private bool ArriveNow()
        {
            var ship = Ship();
            return ship != null && _voyage.MoveDestination(ship.Transform.position);
        }

        private bool Sink()
        {
            var controller = (Ship() as IShipState)?.Controller;
            if (controller == null || !controller.TryGetHealth(out var health)) return false;
            controller.SetAttribute(health.HealthDef, new AttributeValue(0f));
            return true;
        }

        private ScenarioCheck Press(string itemId)
        {
            if (!_menus.IsOpen) return ScenarioCheck.Fail("no menu open on this peer");
            string menu = _menus.Current?.MenuId ?? "?";
            if (_menus.Current?.Items.All(row => row.ItemId != itemId) == true) return ScenarioCheck.Fail($"menu '{menu}' has no row '{itemId}'");
            _menus.Invoke(itemId);
            return ScenarioCheck.Pass($"pressed '{itemId}' in '{menu}'");
        }

        /// <summary>This peer: remember the voyage number, so <c>VoyageAdvanced</c> sees a restart however fast it runs.</summary>
        private ScenarioCheck Record()
        {
            var state = _actors.GetActors<IVoyageState>().FirstOrDefault();
            if (state == null) return ScenarioCheck.Fail("no voyage state on this peer");
            _recordedVoyage = state.Voyage;
            return ScenarioCheck.Pass($"voyage {_recordedVoyage} recorded");
        }

        private ScenarioCheck CheckAdvanced()
        {
            var state = _actors.GetActors<IVoyageState>().FirstOrDefault();
            if (state == null) return ScenarioCheck.Fail("no voyage state on this peer");
            string detail = $"voyage {state.Voyage} ({_recordedVoyage} recorded) on this peer";
            return state.Voyage > _recordedVoyage ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckPhase(string phase)
        {
            var state = _actors.GetActors<IVoyageState>().FirstOrDefault();
            if (state == null) return ScenarioCheck.Fail("no voyage state on this peer");
            string detail = $"voyage {state.Phase} on this peer";
            return string.Equals(state.Phase.ToString(), phase, StringComparison.OrdinalIgnoreCase)
                ? ScenarioCheck.Pass(detail)
                : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckMenu(string menuId)
        {
            string showing = _menus.IsOpen ? _menus.Current?.MenuId ?? "?" : "none";
            string detail = $"menu {showing} on this peer";
            return showing == menuId ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
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
