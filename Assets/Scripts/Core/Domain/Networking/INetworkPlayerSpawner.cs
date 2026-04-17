using UnityEngine;

namespace TinCan.Core.Domain.Networking
{
    /// <summary>
    /// Domain Layer: Interface for spawning players in a networked environment.
    /// Ensures that spawned objects are correctly injected with dependencies.
    /// </summary>
    public interface INetworkPlayerSpawner
    {
        event System.Action<GameObject, ulong, bool> OnPlayerSpawned;
        void SpawnPlayer(ulong clientId, GameObject prefab, bool isServer, bool isLocalPlayer = false);
        void NotifyPlayerSpawned(GameObject instance, ulong clientId, bool isLocalPlayer);
    }
}
