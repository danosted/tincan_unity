using Unity.Netcode;
using TinCan.Core.Domain;

namespace TinCan.Core.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Unified time service that bridges Unity Time and Netcode ServerTime.
    /// Provides synchronized time when in a network session, and falls back to local time otherwise.
    /// The simulation tick comes from <see cref="Network.Infrastructure.NetworkSimulationScheduler"/>.
    /// </summary>
    public class ProjectTimeService : ITimeService
    {
        private readonly NetworkManager _networkManager;
        private float? _simulationDeltaTime;
        private int _tick;

        public ProjectTimeService(NetworkManager networkManager)
        {
            _networkManager = networkManager;
        }

        public float Time => _networkManager.IsListening
            ? (float)_networkManager.ServerTime.Time
            : UnityEngine.Time.time;

        public float DeltaTime => _simulationDeltaTime ?? UnityEngine.Time.deltaTime;
        public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
        public int Tick => _tick;
        public int TickRate => (int)_networkManager.NetworkConfig.TickRate;

        public void BeginSimulationTick(int tick, uint tickRate)
        {
            _tick = tick;
            _simulationDeltaTime = 1f / tickRate;
        }

        public void EndSimulationTick()
        {
            _simulationDeltaTime = null;
        }
    }
}
