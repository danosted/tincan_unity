#nullable enable
using System;
using Unity.Netcode;
using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Infrastructure;
using TinCan.Features.Airship;
using TinCan.Network.Infrastructure;

namespace TinCan.Features.Airship.Infrastructure
{
    /// <summary>
    /// Dedicated mediator for client requests to place a build module.
    /// Keeps Netcode logic out of the view and routes placement requests to the server.
    /// </summary>
    [RequireComponent(typeof(NetworkBehaviour))]
    public class BuildPlacementNetworkMediator : NetworkMediator, IBuildPlacementRequestor
    {
        public void RequestPlacement(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, IActor parentShip)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[BuildPlacementNetworkMediator] Cannot request placement with null prefab.");
                return;
            }

            if (IsServer)
            {
                PlaceModuleOnServer(prefab, worldPosition, worldRotation, parentShip);
                return;
            }

            if (!IsOwner)
            {
                Debug.LogWarning("[BuildPlacementNetworkMediator] Only the owner can request module placement.");
                return;
            }

            if (parentShip is not MonoBehaviour shipMono || !shipMono.TryGetComponent(out NetworkObject shipNetObj))
            {
                Debug.LogWarning("[BuildPlacementNetworkMediator] Parent ship is not networked or not a MonoBehaviour.");
                return;
            }

            var shipRef = new NetworkObjectReference(shipNetObj);
            var localPosition = shipNetObj.transform.InverseTransformPoint(worldPosition);
            var localRotation = Quaternion.Inverse(shipNetObj.transform.rotation) * worldRotation;
            RequestPlacementServerRpc(prefab.name, localPosition, localRotation, shipRef);
        }

        [Rpc(SendTo.Server)]
        private void RequestPlacementServerRpc(string prefabName, Vector3 shipLocalPosition, Quaternion shipLocalRotation, NetworkObjectReference shipRef, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            IActor? parentShip = null;
            var worldPosition = Vector3.zero;
            var worldRotation = Quaternion.identity;
            if (shipRef.TryGet(out NetworkObject shipNetObj))
            {
                parentShip = shipNetObj.GetComponentInParent<IActor>();
                worldPosition = shipNetObj.transform.TransformPoint(shipLocalPosition);
                worldRotation = shipNetObj.transform.rotation * shipLocalRotation;
            }
            else
            {
                Debug.LogWarning("[BuildPlacementNetworkMediator] Server could not resolve ship reference for placement.");
                return;
            }

            var prefab = FindBuildPrefab(prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[BuildPlacementNetworkMediator] Server could not find build prefab named '{prefabName}'.");
                return;
            }

            PlaceModuleOnServer(prefab, worldPosition, worldRotation, parentShip);
        }

        private void PlaceModuleOnServer(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, IActor parentShip)
        {
            var scope = UnityEngine.Object.FindAnyObjectByType<ProjectLifetimeScope>();
            if (scope == null || scope.Container == null)
            {
                Debug.LogWarning("[BuildPlacementNetworkMediator] ProjectLifetimeScope not found on server.");
                return;
            }

            if (!scope.Container.TryResolve(typeof(IModulePlacementUseCase), out var resolvedPlacementUseCase))
            {
                Debug.LogWarning("[BuildPlacementNetworkMediator] IModulePlacementUseCase not registered in container.");
                return;
            }

            if (resolvedPlacementUseCase is not IModulePlacementUseCase placementUseCase)
            {
                Debug.LogWarning("[BuildPlacementNetworkMediator] Resolved placement use case is not the expected type.");
                return;
            }

            placementUseCase.RequestPlacement(prefab, worldPosition, worldRotation, parentShip);
        }

        private GameObject? FindBuildPrefab(string prefabName)
        {
            // Resolve by name from loaded assets. This is a temporary lookup until build prefabs are exposed through a dedicated registry.
            var allPrefabs = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var prefab in allPrefabs)
            {
                if (prefab == null) continue;
                if (prefab.name == prefabName || prefab.name == prefabName.Replace("_Ghost", string.Empty))
                {
                    return prefab;
                }
            }
            return null;
        }
    }
}
