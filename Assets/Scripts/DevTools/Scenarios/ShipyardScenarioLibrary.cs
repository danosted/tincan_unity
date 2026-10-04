#nullable enable
using System.Globalization;
using System.Linq;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the shipyard, on the peer that runs them (the shipyard is local; the host uses it). Commands:
    /// open, place a part ("partId x y z"), save, new, load, launch, and forget a saved design (deletes its file).
    /// Probe: ShipyardParts, the open design has this many parts. Plan: .docs/plans/modular-airship-builder.md (S3).
    /// </summary>
    public sealed class ShipyardScenarioLibrary : IScenarioLibrary
    {
        private readonly IShipyard _shipyard;
        private readonly IShipDesignStore _store;

        public ShipyardScenarioLibrary(IShipyard shipyard, IShipDesignStore store)
        {
            _shipyard = shipyard;
            _store = store;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("ShipyardOpen", _ => Do(() => _shipyard.Open(), "opened")),
            new ScenarioCommand("ShipyardPlace", Place),
            new ScenarioCommand("ShipyardSave", name => Check(_shipyard.Save(name))),
            new ScenarioCommand("ShipyardNew", _ => Do(_shipyard.New, "new design")),
            new ScenarioCommand("ShipyardLoad", name => Check(_shipyard.Load(name))),
            new ScenarioCommand("ShipyardLaunch", _ => Check(_shipyard.Launch())),
            new ScenarioCommand("ShipyardForget", name => _store.TryDelete(name)
                ? ScenarioCheck.Pass($"deleted {name}")
                : ScenarioCheck.Fail($"no saved design {name}")),
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("ShipyardParts", CheckParts),
        };

        private ScenarioCheck Place(string argument)
        {
            var words = argument.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 4) return ScenarioCheck.Fail("expected \"partId x y z\"");
            if (!_shipyard.Select(words[0])) return ScenarioCheck.Fail($"no part {words[0]}");

            var xyz = words.Skip(1).Select(w => int.Parse(w, CultureInfo.InvariantCulture)).ToArray();
            return Check(_shipyard.PlaceAt(new ShipGridCell(xyz[0], xyz[1], xyz[2])));
        }

        private ScenarioCheck CheckParts(string count)
        {
            var design = _shipyard.Design;
            if (design == null) return ScenarioCheck.Fail("no design open");

            string detail = $"\"{design.Name}\" has {design.Parts.Count} parts";
            return design.Parts.Count == int.Parse(count, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck Check(bool done) => done ? ScenarioCheck.Pass(_shipyard.Message) : ScenarioCheck.Fail(_shipyard.Message);

        private static ScenarioCheck Do(System.Action action, string detail)
        {
            action();
            return ScenarioCheck.Pass(detail);
        }
    }
}
