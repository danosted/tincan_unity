using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using System;

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
        public IInteractable CurrentTarget { get; private set; }

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => false; // Just scanning, not doing physical simulation

        // Needs to be tied to possession to know if the local player is currently controlling this actor
        public bool IsCapturedBy(ulong playerId)
        {
            var possessable = GetComponent<Possession.IPossessable>();
            return possessable != null && possessable.IsCapturedBy(playerId);
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
        }

        // Draw debug line in editor
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = CurrentTarget != null ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, transform.forward * _interactionRange);
        }
    }
}
