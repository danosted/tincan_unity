#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Features.ShipDesigns;
using TinCan.Features.ShipPicker;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the temporary ship picker (.docs/plans/ship-picker-temporary.md). Commands: PickShip "key" picks a
    /// stored design as its menu row does (host; menu clicks are not scriptable). Probes: ShipFlies "key": the ship's
    /// replicated design is that stored design, and this peer has built it.
    /// </summary>
    public sealed class ShipPickerScenarioLibrary : IScenarioLibrary
    {
        private readonly IShipPicker _picker;
        private readonly IShipDesignStore _store;
        private readonly IShipAssembly _assembly;
        private readonly IActorRegistry _actors;

        public ShipPickerScenarioLibrary(IShipPicker picker, IShipDesignStore store, IShipAssembly assembly, IActorRegistry actors)
        {
            _picker = picker;
            _store = store;
            _assembly = assembly;
            _actors = actors;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PickShip", key => _picker.Pick(key, out var message) ? ScenarioCheck.Pass(message) : ScenarioCheck.Fail(message)),
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("ShipFlies", CheckFlies),
        };

        private ScenarioCheck CheckFlies(string key)
        {
            var loaded = _store.Load(key);
            if (!loaded.Succeeded) return ScenarioCheck.Fail($"\"{key}\" does not load: {loaded.Error}");
            ulong expected = ShipDesignHash.Compute(loaded.Design!);

            var state = _actors.GetActors<IShipDesignState>().FirstOrDefault(s => s.Ship != null);
            if (state == null) return ScenarioCheck.Fail("no ship design state on a ship");
            if (!_assembly.TryGetBuilt(state.Ship!.Id, out var hash, out var parts)) return ScenarioCheck.Fail("nothing built yet");

            string detail = $"built {ShipDesignHash.ToText(hash)} with {parts} parts, replicated {ShipDesignHash.ToText(state.DesignHash)}, " +
                $"\"{key}\" is {ShipDesignHash.ToText(expected)}";
            return state.DesignHash == expected && hash == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }
    }
}
