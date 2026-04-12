using UnityEngine;

namespace TinCan.Core.Domain.Networking
{
    /// <summary>
    /// Domain Layer: Interface for spawning players in a networked environment.
    /// Ensures that spawned objects are correctly injected with dependencies.
    /// </summary>
    public interface INetworkPlayerSpawner
    {
        void SpawnPlayer(ulong clientId, GameObject prefab);
    }
}
