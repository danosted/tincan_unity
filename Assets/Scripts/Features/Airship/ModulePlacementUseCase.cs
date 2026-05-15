using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;

namespace TinCan.Features.Airship
{
    public class ModulePlacementUseCase : IModulePlacementUseCase
    {
        private readonly IModuleSpawningService _spawningService;

        public ModulePlacementUseCase(IModuleSpawningService spawningService)
        {
            _spawningService = spawningService;
        }

        public void RequestPlacement(GameObject modulePrefab, Vector3 worldPosition, Quaternion worldRotation, IActor targetShip)
        {
            // Note: Add server-side distance validation here later if needed
            if (modulePrefab == null)
            {
                Debug.LogWarning("[ModulePlacementUseCase] RequestPlacement called with null prefab.");
                return;
            }

            Debug.Log($"[ModulePlacementUseCase] Requesting placement of {modulePrefab.name} on {targetShip?.Id}");
            _spawningService.SpawnModule(modulePrefab, worldPosition, worldRotation, targetShip);
        }
    }
}
