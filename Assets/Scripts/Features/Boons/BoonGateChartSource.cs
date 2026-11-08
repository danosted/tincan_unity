#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Navigation;
using UnityEngine;

namespace TinCan.Features.Boons
{
    /// <summary>
    /// Every peer: the voyage's boon gates on the navigator's chart (<see cref="ChartObstacleKind.Boon"/>), built from the
    /// seed like the gates themselves (<see cref="BoonGateMap"/>), so a route can be plotted through them. A used gate
    /// stays on the chart; the chart shows where gates were laid, not which are left.
    /// </summary>
    public class BoonGateChartSource : IChartSource
    {
        private readonly BoonGateMap _gates;
        private readonly BoonsConfig _config;

        public BoonGateChartSource(BoonGateMap gates, BoonsConfig config)
        {
            _gates = gates;
            _config = config;
        }

        public void CollectPointsOfInterest(ICollection<PointOfInterest> into) { }

        public void CollectObstacles(Vector3 center, float radius, ICollection<ChartObstacle> into)
        {
            foreach (var gate in _gates.Gates)
            {
                var definition = _config.GateAt(gate.GateIndex);
                if (definition == null) continue;

                float reach = definition.Radius;
                float dx = gate.Position.x - center.x, dz = gate.Position.z - center.z;
                float limit = radius + reach;
                if (dx * dx + dz * dz > limit * limit) continue;
                into.Add(new ChartObstacle(gate.Position, reach, gate.Position.y - reach, gate.Position.y + reach, ChartObstacleKind.Boon));
            }
        }
    }
}
