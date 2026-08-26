using Unity.Netcode;
using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Core.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Unified time service that bridges Unity Time and Netcode ServerTime.
    /// Provides synchronized time when in a network session, and falls back to local time otherwise.
    /// </summary>
    public class ProjectTimeService : ITimeService
    {
        private float? _simulationDeltaTime;

        public float Time
        {
            get
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    return (float)NetworkManager.Singleton.ServerTime.Time;
                }
                return UnityEngine.Time.time;
            }
        }

        public float DeltaTime => _simulationDeltaTime ?? UnityEngine.Time.deltaTime;
        public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;

        public void BeginSimulationTick(uint tickRate)
        {
            _simulationDeltaTime = 1f / tickRate;
        }

        public void EndSimulationTick()
        {
            _simulationDeltaTime = null;
        }
    }
}
