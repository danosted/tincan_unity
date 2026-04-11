using Unity.Netcode;
using UnityEngine;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Base class for networking mediators.
    /// Bridges the gap between Domain Use Cases and the Networking Library (NGO).
    /// </summary>
    public abstract class NetworkMediator : NetworkBehaviour
    {
        // Future shared logic for latency compensation, ownership transfer, etc.
    }
}
