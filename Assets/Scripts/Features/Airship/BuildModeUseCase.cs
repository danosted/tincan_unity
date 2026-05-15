#nullable enable
using UnityEngine;
using Unity.Netcode;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using VContainer.Unity;
using TinCan.Features.FreeCamera;

namespace TinCan.Features.Airship
{
    public class BuildModeUseCase : ITickable
    {
        private readonly IModulePlacementUseCase _placementUseCase;
        private readonly IActorRegistry _actorRegistry;
        private readonly IInputService _inputService; // Add this field
        private readonly GameplayTag? _buildingTag;

        private GameObject? _ghostInstance;
        private IActor? _targetShip;
        private Vector3 _validPosition;
        private Quaternion _validRotation;

        public bool IsPlacementValid { get; private set; }

        public BuildModeUseCase(
            IModulePlacementUseCase placementUseCase,
            IActorRegistry actorRegistry,
            IInputService inputService,
            GameplayTag? buildingTag)
        {
            _placementUseCase = placementUseCase;
            _actorRegistry = actorRegistry;
            _inputService = inputService;
            _buildingTag = buildingTag;
            if (_buildingTag != null)
            {
                Debug.Log($"[BuildModeUseCase] Initialized. Building Tag Name: {_buildingTag.name}");
            }
            else
            {
                Debug.LogError("[BuildModeUseCase] Initialization Error: Building Tag is null!");
            }
        }

        public void Tick()
        {
            var localPlayer = _actorRegistry.GetLocalPlayerActor<IAbilityControllerBase>();
            if (localPlayer == null) return;

            // Handle Build Mode Toggle
            if (_inputService.WasActionTriggered(ActionNames.BuildMode))
            {
                if (_buildingTag != null)
                {
                    if (localPlayer.HasTag(_buildingTag))
                    {
                        localPlayer.RemoveTag(_buildingTag);
                        Debug.Log("[BuildModeUseCase] Exiting Build Mode (Key: B)");
                    }
                    else
                    {
                        localPlayer.AddTag(_buildingTag);
                        Debug.Log("[BuildModeUseCase] Entering Build Mode (Key: B)");
                    }
                }
            }
            // Also allow exiting with Cancel (Escape) if already in build mode
            else if (_inputService.WasActionTriggered(ActionNames.Cancel) && _buildingTag != null && localPlayer.HasTag(_buildingTag))
            {
                localPlayer.RemoveTag(_buildingTag);
                Debug.Log("[BuildModeUseCase] Exiting Build Mode (Key: Cancel)");
            }

            bool isBuilding = localPlayer.HasTag(_buildingTag);

            if (isBuilding)
            {
                UpdateGhost(localPlayer);

                // Replace Input.GetMouseButtonDown(0) with the abstracted call
                if (_inputService.WasActionTriggered(ActionNames.AbilityPrimary) && IsPlacementValid)
                {
                    ConfirmPlacement(localPlayer);
                }
            }
            else
            {
                ClearGhost();
            }
        }
        private void UpdateGhost(IAbilityControllerBase localPlayer)
        {
            // Try to get the selected prefab from the player (placeholder)
            GameObject? selectedPrefab = null;
            if (localPlayer is MonoBehaviour monoPlayer)
            {
                var builder = monoPlayer.GetComponent<IBuilder>();
                if (builder != null) selectedPrefab = builder.SelectedModulePrefab;
            }

            if (selectedPrefab == null || localPlayer is not IHasOrbitalCamera camera)
            {
                ClearGhost();
                return;
            }

            if (_ghostInstance == null || _ghostInstance.name != selectedPrefab.name + "_Ghost")
            {
                ClearGhost();
                _ghostInstance = Object.Instantiate(selectedPrefab);
                _ghostInstance.name = selectedPrefab.name + "_Ghost";

                // Strip everything except visuals and our feedback script to avoid dependency errors
                var allComponents = _ghostInstance.GetComponentsInChildren<Component>(true);

                // Unity requires you to destroy dependent components first.
                // We'll destroy specific known NetworkBehaviours first, then generic ones, then colliders.
                // First pass: Dependent Mediators
                foreach (var comp in allComponents)
                {
                    if (comp is Network.Infrastructure.ShipModuleNetworkMediator) Object.DestroyImmediate(comp);
                }

                // Refresh list
                allComponents = _ghostInstance.GetComponentsInChildren<Component>(true);
                foreach (var comp in allComponents)
                {
                    if (comp is Transform ||
                        comp is Renderer ||
                        comp is MeshFilter ||
                        comp is GhostMaterialFeedback ||
                        comp is Canvas || // Keep UI if it exists
                        comp is UnityEngine.UI.Graphic)
                    {
                        continue;
                    }

                    Object.DestroyImmediate(comp);
                }
            }

            // In third-person, the camera looks at the player, but the ray should still go forward from the camera's perspective.
            // If the camera is managed by an IHasOrbitalCamera, we should try to use its active camera.
            var mainCam = camera.Look.Camera ?? Camera.main;
            if (mainCam == null) return;

            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);

            // Debug the raycast to see if it's hitting anything
            Debug.DrawRay(ray.origin, ray.direction * 20f, Color.yellow);

            // Layer 8 is our "Ship" layer. We should explicitly cast against it to avoid hitting the player's own colliders
            int layerMask = 1 << 8;

            if (Physics.Raycast(ray, out RaycastHit hit, 20f, layerMask))
            {
                _targetShip = hit.collider.GetComponentInParent<IActor>();
                if (_targetShip != null)
                {
                    IsPlacementValid = true;
                    _validPosition = hit.point;
                    _validRotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(90, 0, 0); // Align up with normal

                    // If the ghost is far away (e.g. just spawned), snap it immediately instead of lerping from (0,0,0)
                    if (Vector3.Distance(_ghostInstance.transform.position, _validPosition) > 5f)
                    {
                        _ghostInstance.transform.position = _validPosition;
                        _ghostInstance.transform.rotation = _validRotation;
                    }
                    else
                    {
                        // Smooth the ghost movement for a more natural feel
                        _ghostInstance.transform.position = Vector3.Lerp(_ghostInstance.transform.position, _validPosition, Time.deltaTime * 20f);
                        _ghostInstance.transform.rotation = Quaternion.Slerp(_ghostInstance.transform.rotation, _validRotation, Time.deltaTime * 15f);
                    }

                    _ghostInstance.SetActive(true);
                    return;
                }
            }

            // Invalid placement - If we aren't looking at the ship, hide the ghost
            IsPlacementValid = false;
            if (_ghostInstance != null) _ghostInstance.SetActive(false);
            _targetShip = null;
        }

        private void ConfirmPlacement(IAbilityControllerBase localPlayer)
        {
            if (_targetShip == null || _ghostInstance == null) return;
            if (localPlayer is not MonoBehaviour monoPlayer) return;

            // Optional: End the building ability here if you want it to close after placing one item
            // localPlayer.RemoveTag(_buildingTag); // This would end the toggle

            GameObject? prefabToSpawn = null;
            IBuilder? builder = null;


            builder = monoPlayer.GetComponent<IBuilder>();
            if (builder != null) prefabToSpawn = builder.SelectedModulePrefab;


            if (prefabToSpawn != null)
            {
                var placementRequester = monoPlayer.GetComponent<IBuildPlacementRequestor>();
                if (placementRequester != null)
                {
                    placementRequester.RequestPlacement(prefabToSpawn, _validPosition, _validRotation, _targetShip);
                }
                else
                {
                    _placementUseCase.RequestPlacement(prefabToSpawn, _validPosition, _validRotation, _targetShip);
                }
            }
        }

        private void ClearGhost()
        {
            if (_ghostInstance != null)
            {
                Object.Destroy(_ghostInstance);
                _ghostInstance = null;
            }
            IsPlacementValid = false;
            _targetShip = null;
        }
    }
}
