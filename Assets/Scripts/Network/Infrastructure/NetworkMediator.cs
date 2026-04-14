using Unity.Netcode;
using UnityEngine;
using TinCan.Core.Domain;
using VContainer;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Base class for networking mediators.
    /// Bridges the gap between Domain Use Cases and the Networking Library (NGO).
    /// Handles automatic registration with the IActorRegistry.
    /// </summary>
    public abstract class NetworkMediator : NetworkBehaviour
    {
        protected IActorRegistry Registry { get; private set; }

        [Inject]
        public void Construct(IActorRegistry registry)
        {
            Registry = registry;
        }
    }
}
