using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Features.Interaction;

namespace TinCan.Core.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Orchestrates the link between Unity GameObjects and Domain Registries.
    /// This ensures that when an actor is created (via Spawner, Interceptor, or Scene Load),
    /// all its capabilities are registered in the correct places.
    /// </summary>
    public class ActorOrchestrator : IActorOrchestrator
    {
        private readonly IActorRegistry _actorRegistry;
        private readonly IInteractorRegistry _interactorRegistry;
        private readonly IAbilityRegistry _abilityRegistry;

        public ActorOrchestrator(IActorRegistry actorRegistry, IInteractorRegistry interactorRegistry, IAbilityRegistry abilityRegistry)
        {
            _actorRegistry = actorRegistry;
            _interactorRegistry = interactorRegistry;
            _abilityRegistry = abilityRegistry;
        }

        public void RegisterHierarchy(GameObject root)
        {
            // Register Identity
            if (root.TryGetComponent<IActor>(out var actor))
            {
                _actorRegistry.Register(actor);
            }

            // Register Capabilities
            var interactors = root.GetComponentsInChildren<IInteractorView>(true);
            foreach (var interactor in interactors)
            {
                _interactorRegistry.Register(interactor);
            }

            var abilityControllers = root.GetComponentsInChildren<IAbilityControllerBase>(true);
            foreach (var controller in abilityControllers)
            {
                _abilityRegistry.Register(controller);
            }
        }

        public void UnregisterHierarchy(GameObject root)
        {
            // Unregister Identity
            if (root.TryGetComponent<IActor>(out var actor))
            {
                _actorRegistry.Unregister(actor);
            }

            // Unregister Capabilities
            var interactors = root.GetComponentsInChildren<IInteractorView>(true);
            foreach (var interactor in interactors)
            {
                _interactorRegistry.Unregister(interactor);
            }

            var abilityControllers = root.GetComponentsInChildren<IAbilityControllerBase>(true);
            foreach (var controller in abilityControllers)
            {
                _abilityRegistry.Unregister(controller);
            }
        }
    }
}
