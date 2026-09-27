using UnityEngine;
using TinCan.Core.Domain;
using VContainer;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Infrastructure Layer: the owner's interaction prompt. It runs the same targeting query the server runs when
    /// Interact is pressed (TD_Interact), so what the player sees is what the server acts on. Needs the Interaction and
    /// Targeting installers; without them there is no target.
    /// </summary>
    public class InteractorControllerView : MonoBehaviour, IInteractorView
    {
        private IActor _owner;
        private Targeting.ITargetingService _targeting;
        private InteractionTargetingSettings _settings;
        private bool _hasQuery;
        private TinCan.Core.Domain.Targeting.TargetingOrigin _lastOrigin;
        private TinCan.Core.Domain.Targeting.ITargetable _lastTarget;

        public IInteractable CurrentTarget { get; private set; }
        public IActor Owner => _owner;

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

        private void Update()
        {
            ScanForInteractables();
        }

        private void ScanForInteractables()
        {
            if (_targeting == null || _settings == null || _owner is not HumanoidMovement.IHumanoidCharacterView character)
            {
                CurrentTarget = null;
                _hasQuery = false;
                return;
            }

            var targeter = new Targeting.HumanoidTargeter(character);
            bool acquired = _targeting.TryAcquire(targeter, _settings.Targeting, out var result);
            CurrentTarget = acquired ? result.Target as IInteractable : null;

            // Visualise exactly what TD_Interact saw: its ray from its source, and the acquired target.
            _hasQuery = targeter.TryGetOrigin(out _lastOrigin);
            _lastTarget = acquired ? result.Target : null;
            if (_hasQuery) Targeting.TargetingGizmos.DrawDebug(_lastOrigin, _settings.Targeting, _lastTarget);
        }

        // Scene-view gizmo: the last targeting query.
        private void OnDrawGizmos()
        {
            if (_hasQuery && _settings != null) Targeting.TargetingGizmos.DrawGizmos(_lastOrigin, _settings.Targeting, _lastTarget);
        }
    }
}
