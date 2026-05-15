using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Core.Domain.Networking
{
    public interface IModuleSpawningService
    {
        void SpawnModule(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, IActor parentShip);
    }
}
