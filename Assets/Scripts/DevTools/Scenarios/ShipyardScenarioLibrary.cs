#nullable enable
using System.Globalization;
using System.Linq;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the shipyard, on the peer that runs them (the shipyard is local; the host uses it). Commands:
    /// open, place a part as a click straight down would (ShipyardPlaceDown "partId x z", metres in ship space: a face part
    /// on the plank floor there, snapped to the frame step; a node part on the node there), save, new, load, launch,
    /// and forget a saved design (deletes its file); the frame tools: ShipyardMode "Parts" or "Frame", ShipyardFrameClick,
    /// ShipyardFrameFill, ShipyardFrameRailing and ShipyardFrameStairs "x z" (metres in ship space: straight down there).
    /// Probes: ShipyardParts, the open design has this many parts; ShipyardFrame "nodes edges faces", its frame has these
    /// counts. Plans: .docs/plans/modular-airship-builder.md (S3), .docs/plans/ship-frames.md (S2, S4),
    /// .docs/plans/frame-only-ships.md.
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
            new ScenarioCommand("ShipyardPlaceDown", PlaceDown),
            new ScenarioCommand("ShipyardSave", name => Check(_shipyard.Save(name))),
            new ScenarioCommand("ShipyardNew", _ => Do(_shipyard.New, "new design")),
            new ScenarioCommand("ShipyardLoad", name => Check(_shipyard.Load(name))),
            new ScenarioCommand("ShipyardLaunch", _ => Check(_shipyard.Launch())),
            new ScenarioCommand("ShipyardForget", name => _store.TryDelete(name)
                ? ScenarioCheck.Pass($"deleted {name}")
                : ScenarioCheck.Fail($"no saved design {name}")),
            new ScenarioCommand("ShipyardMode", SetMode),
            new ScenarioCommand("ShipyardFrameClick", point => Check(_shipyard.FrameClickAt(Point(point)))),
            new ScenarioCommand("ShipyardFrameFill", point => Check(_shipyard.FrameFillAt(Point(point)))),
            new ScenarioCommand("ShipyardFrameRailing", point => Check(_shipyard.FrameRailingAt(Point(point)))),
            new ScenarioCommand("ShipyardFrameStairs", point => Check(_shipyard.FrameStairsAt(Point(point)))),
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("ShipyardParts", CheckParts),
            new ScenarioProbe("ShipyardFrame", CheckFrame),
        };

        private ScenarioCheck PlaceDown(string argument)
        {
            var words = argument.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 3) return ScenarioCheck.Fail("expected \"partId x z\" (metres)");
            if (!_shipyard.Select(words[0])) return ScenarioCheck.Fail($"no part {words[0]}");
            return Check(_shipyard.PlaceDownAt(Point($"{words[1]} {words[2]}")));
        }

        private ScenarioCheck CheckParts(string count)
        {
            var design = _shipyard.Design;
            if (design == null) return ScenarioCheck.Fail("no design open");

            string detail = $"\"{design.Name}\" has {design.Parts.Count} parts";
            return design.Parts.Count == int.Parse(count, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck SetMode(string mode)
        {
            if (!System.Enum.TryParse<ShipyardMode>(mode, out var parsed)) return ScenarioCheck.Fail($"no mode {mode}");
            _shipyard.SetMode(parsed);
            return ScenarioCheck.Pass($"mode {parsed}");
        }

        /// <summary>"x z" in metres: a point on the ship's floor plan (the frame tools find the height).</summary>
        private static UnityEngine.Vector3 Point(string argument)
        {
            var xz = argument.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Select(w => float.Parse(w, CultureInfo.InvariantCulture)).ToArray();
            return new UnityEngine.Vector3(xz[0], 0f, xz[1]);
        }

        private ScenarioCheck CheckFrame(string counts)
        {
            var frame = _shipyard.Design?.Frame;
            if (frame == null) return ScenarioCheck.Fail("no design open");

            string detail = $"{frame.Nodes.Count} {frame.Edges.Count} {frame.Faces.Count}";
            return detail == string.Join(" ", counts.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
                ? ScenarioCheck.Pass($"frame {detail}")
                : ScenarioCheck.Fail($"frame {detail} (nodes edges faces)");
        }

        private ScenarioCheck Check(bool done) => done ? ScenarioCheck.Pass(_shipyard.Message) : ScenarioCheck.Fail(_shipyard.Message);

        private static ScenarioCheck Do(System.Action action, string detail)
        {
            action();
            return ScenarioCheck.Pass(detail);
        }
    }
}
