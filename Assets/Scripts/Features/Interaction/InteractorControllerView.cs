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
        private Targeting.ITargetingService _targeting;
        private InteractionTargetingSettings _settings;
        private bool _hasQuery;
        private TinCan.Core.Domain.Targeting.TargetingOrigin _lastOrigin;
        private TinCan.Core.Domain.Targeting.ITargetable _lastTarget;

        public IInteractable CurrentTarget { get; private set; }
        public IActor Owner => _owner;

        // Optional: with the Interaction and Targeting installers active, the prompt runs the same targeting query the
        // server runs when Interact is pressed (TD_Interact), so what the player sees is what the server acts on.
        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            resolver.TryResolve(out _targeting);
            resolver.TryResolve(out _settings);
        }

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
            if (_targeting != null && _settings != null && _owner is HumanoidMovement.IHumanoidCharacterView character)
            {
                var targeter = new Targeting.HumanoidTargeter(character);
                bool acquired = _targeting.TryAcquire(targeter, _settings.Targeting, out var result);
                CurrentTarget = acquired ? result.Target as IInteractable : null;

                // Visualise exactly what TD_Interact saw: its ray from its source, and the acquired target.
                _hasQuery = targeter.TryGetOrigin(out _lastOrigin);
                _lastTarget = acquired ? result.Target : null;
                if (_hasQuery) Targeting.TargetingGizmos.DrawDebug(_lastOrigin, _settings.Targeting, _lastTarget);
                return;
            }

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

        // Scene-view gizmo: the last targeting query when interaction runs on targeting, else the legacy flat ray.
        private void OnDrawGizmos()
        {
            if (_hasQuery && _settings != null)
            {
                Targeting.TargetingGizmos.DrawGizmos(_lastOrigin, _settings.Targeting, _lastTarget);
                return;
            }

            Gizmos.color = CurrentTarget != null ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, transform.forward * _interactionRange);
        }
    }
}
