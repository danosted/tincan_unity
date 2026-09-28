#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Where in the fixed network tick a feature runs, relative to the core movement simulations. Order in a tick:
    /// airship movement, <see cref="AfterAirship"/>, <see cref="BeforeHumanoid"/>, physics sync, humanoid movement,
    /// <see cref="AfterHumanoid"/>.
    /// </summary>
    public enum SimulationPhase
    {
        /// <summary>After airship movement, before physics sync and humanoid movement.</summary>
        AfterAirship = 0,
        /// <summary>After humanoid movement (the last thing in the tick).</summary>
        AfterHumanoid = 1,
        /// <summary>After every <see cref="AfterAirship"/> feature, just before physics sync and humanoid movement
        /// (the cloud boundary corrects the ship and players here).</summary>
        BeforeHumanoid = 2
    }

    /// <summary>
    /// A feature use case that must run on the fixed network tick. Register it with <c>.As&lt;ISimulationTickable&gt;()</c>
    /// from a FeatureInstaller; NetworkSimulationScheduler runs all of them by phase in registration order.
    /// </summary>
    public interface ISimulationTickable
    {
        SimulationPhase Phase { get; }
        void Tick();
    }

    /// <summary>Pure helper that groups tickables by phase so the scheduler stays a thin transport.</summary>
    public sealed class SimulationTickRunner
    {
        private readonly Dictionary<SimulationPhase, List<ISimulationTickable>> _byPhase;

        public SimulationTickRunner(IEnumerable<ISimulationTickable> tickables)
        {
            _byPhase = tickables.Where(t => t != null)
                .GroupBy(t => t.Phase)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        public int Count => _byPhase.Values.Sum(list => list.Count);

        public void Run(SimulationPhase phase)
        {
            if (!_byPhase.TryGetValue(phase, out var list)) return;
            for (int i = 0; i < list.Count; i++) list[i].Tick();
        }
    }
}
