#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Core.Domain
{
    /// <summary>Where in the fixed network tick a feature runs, relative to the core movement simulations.</summary>
    public enum SimulationPhase
    {
        /// <summary>After airship movement, before physics sync and humanoid movement.</summary>
        AfterAirship = 0,
        /// <summary>After humanoid movement (the last thing in the tick).</summary>
        AfterHumanoid = 1
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
        private readonly List<ISimulationTickable> _afterAirship;
        private readonly List<ISimulationTickable> _afterHumanoid;

        public SimulationTickRunner(IEnumerable<ISimulationTickable> tickables)
        {
            var all = tickables.Where(t => t != null).ToList();
            _afterAirship = all.Where(t => t.Phase == SimulationPhase.AfterAirship).ToList();
            _afterHumanoid = all.Where(t => t.Phase == SimulationPhase.AfterHumanoid).ToList();
        }

        public int Count => _afterAirship.Count + _afterHumanoid.Count;

        public void Run(SimulationPhase phase)
        {
            var list = phase == SimulationPhase.AfterAirship ? _afterAirship : _afterHumanoid;
            for (int i = 0; i < list.Count; i++) list[i].Tick();
        }
    }
}
