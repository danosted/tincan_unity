#nullable enable
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Ship;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario probes for designed ships. ShipDesignBuilt: this peer has built the ship from the design its state
    /// replicates, with at least the given number of parts standing. SubjectOnShip: the ground under the subject on this
    /// peer belongs to the ship (a built part or the helm). ShipTopSpeed: the ship's replicated top speed, set from the
    /// design's parts (S4). Plan: .docs/plans/modular-airship-builder.md.
    /// </summary>
    public sealed class ShipDesignsScenarioLibrary : IScenarioLibrary
    {
        private readonly ScenarioSubject _subject;
        private readonly IActorRegistry _actors;
        private readonly IShipAssembly _assembly;

        public ShipDesignsScenarioLibrary(ScenarioSubject subject, IActorRegistry actors, IShipAssembly assembly)
        {
            _subject = subject;
            _actors = actors;
            _assembly = assembly;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => Enumerable.Empty<ScenarioCommand>();

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("ShipDesignBuilt", CheckBuilt),
            new ScenarioProbe("SubjectOnShip", _ => CheckOnShip()),
            new ScenarioProbe("ShipTopSpeed", CheckTopSpeed),
        };

        private ScenarioCheck CheckBuilt(string minimumParts)
        {
            var state = _actors.GetActors<IShipDesignState>().FirstOrDefault(s => s.Ship != null);
            if (state == null) return ScenarioCheck.Fail("no ship design state on a ship");
            if (state.DesignHash == 0) return ScenarioCheck.Fail("the ship has no design yet");
            if (!_assembly.TryGetBuilt(state.Ship!.Id, out var hash, out var parts)) return ScenarioCheck.Fail("nothing built yet");

            string detail = $"built {ShipDesignHash.ToText(hash)} with {parts} parts (replicated {ShipDesignHash.ToText(state.DesignHash)})";
            bool enough = parts >= int.Parse(string.IsNullOrEmpty(minimumParts) ? "1" : minimumParts, CultureInfo.InvariantCulture);
            return hash == state.DesignHash && enough ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The ship's top speed on this peer (its replicated flight speed attribute) is the given m/s, within 0.2.</summary>
        private ScenarioCheck CheckTopSpeed(string metresPerSecond)
        {
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault();
            if (ship == null) return ScenarioCheck.Fail("no ship");

            float expected = float.Parse(metresPerSecond, CultureInfo.InvariantCulture);
            string detail = $"top speed {ship.MaxForwardSpeed:0.00} m/s";
            return Mathf.Abs(ship.MaxForwardSpeed - expected) <= 0.2f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckOnShip()
        {
            var body = _subject.Body;
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault();
            if (body == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");

            var from = body.position + Vector3.up * 0.5f;
            if (!Physics.Raycast(from, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                return ScenarioCheck.Fail($"nothing under the subject at {body.position}");
            }

            string detail = $"standing on {hit.collider.name}, {hit.distance - 0.5f:0.00} m below the feet pivot";
            return hit.collider.transform.IsChildOf(ship.Transform) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }
    }
}
