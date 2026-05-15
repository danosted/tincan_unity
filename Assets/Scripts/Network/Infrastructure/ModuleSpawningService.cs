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

            var instance = Object.Instantiate(prefab, worldPosition, worldRotation);
            _container.InjectGameObject(instance);

            var netObj = instance.GetComponent<NetworkObject>();
            netObj.Spawn();

            if (parentShip is MonoBehaviour shipMono)
            {
                var shipNetObj = shipMono.GetComponent<NetworkObject>();
                if (shipNetObj != null)
                {
                    netObj.TrySetParent(shipNetObj.transform, true);
                }
            }

            // Notify module it's attached
            var module = instance.GetComponent<IShipModule>();
            module?.OnAttachedToShip(parentShip);
        }
    }
}
