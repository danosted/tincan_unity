using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Core.Domain.Networking
{
    public interface IModuleSpawningService
    {
        /// <summary>Server: spawns the prefab as a networked child of the ship at a world pose; returns the instance (null off the server).</summary>
        GameObject SpawnModule(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, IActor parentShip);

        /// <summary>Server: despawns and destroys a module spawned by <see cref="SpawnModule"/>.</summary>
        void DespawnModule(GameObject module);
    }
}
