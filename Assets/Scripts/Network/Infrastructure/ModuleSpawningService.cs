using Unity.Netcode;
using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using VContainer;
using VContainer.Unity;

namespace TinCan.Network.Infrastructure
{
    public class ModuleSpawningService : IModuleSpawningService
    {
        private readonly NetworkManager _networkManager;
        private readonly IObjectResolver _container;

        public ModuleSpawningService(NetworkManager networkManager, IObjectResolver container)
        {
            _networkManager = networkManager;
            _container = container;
        }

        public void SpawnModule(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, IActor parentShip)
        {
            if (!_networkManager.IsServer) return;

            // Modules live in ship-local space, so bake the requested world pose into ship-local values up front and
            // parent with worldPositionStays = false. NGO then replicates the LOCAL pose (spawn payload, parent sync
            // and late-join synchronization alike). With worldPositionStays = true it replicates the WORLD pose and
            // every client derives its own offset from wherever its interpolated copy of the ship is that frame, which
            // leaves fixtures floating off the ship for clients (worst for late joiners on a moving ship).
            var shipNetObj = parentShip is MonoBehaviour shipMono ? shipMono.GetComponent<NetworkObject>() : null;
            var position = worldPosition;
            var rotation = worldRotation;
            if (shipNetObj != null)
            {
                position = shipNetObj.transform.InverseTransformPoint(worldPosition);
                rotation = Quaternion.Inverse(shipNetObj.transform.rotation) * worldRotation;
            }

            // Unparented, these "world" values are the local values SetParent(worldPositionStays: false) preserves.
            var instance = Object.Instantiate(prefab, position, rotation);
            _container.InjectGameObject(instance);

            var netObj = instance.GetComponent<NetworkObject>();
            netObj.Spawn();

            if (shipNetObj != null)
            {
                netObj.TrySetParent(shipNetObj.transform, false);
            }

            // Notify module it's attached
            var module = instance.GetComponent<IShipModule>();
            module?.OnAttachedToShip(parentShip);
        }
    }
}
