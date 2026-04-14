using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using System;
using VContainer;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Infrastructure Layer: Unity component that scans for IInteractable objects in the world.
    /// Attaches to an actor that can interact, like a Player Character.
    /// </summary>
    public class InteractorControllerView : MonoBehaviour, IInteractorView
    {
        [Header("Interaction Settings")]
        [SerializeField] private float _interactionRange = 3f;
        [SerializeField] private LayerMask _interactableMask = ~0; // Default: hit everything

        private Camera _mainCamera; // Usually we raycast from the camera center in first/third person
        private IActor _owner;

        public IInteractable CurrentTarget { get; private set; }
        public IActor Owner => _owner;

        private void Awake()
        {
            _owner = GetComponentInParent<IActor>();
        }

        private void Start()
        {
            _mainCamera = Camera.main; // Can be improved by linking to the actual possession camera
        }

        private void Update()
        {
            ScanForInteractables();
        }

        private void ScanForInteractables()
        {
            // Note: Raycasting from the actor's forward is straightforward,
            // but in many games (especially with cameras), raycasting from the camera center feels more intuitive.
            // Using transform.forward for now, as requested.
            Ray ray = new Ray(transform.position + Vector3.up * 1.5f, transform.forward); // Assuming eye level

            if (Physics.Raycast(ray, out RaycastHit hit, _interactionRange, _interactableMask))
            {
                // Try to get IInteractable from the hit object or its parents
                var interactable = hit.collider.GetComponentInParent<IInteractable>();

                if (interactable != null)
                {
                    CurrentTarget = interactable;
                }
                else
                {
                    CurrentTarget = null;
                }
            }
            else
            {
                CurrentTarget = null;
            }

            // Runtime debug visualization
            Debug.DrawRay(ray.origin, ray.direction * _interactionRange, CurrentTarget != null ? Color.green : Color.red);
        }

        // Draw debug line in editor
        private void OnDrawGizmos()
        {
            Gizmos.color = CurrentTarget != null ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, transform.forward * _interactionRange);
        }
    }
}
