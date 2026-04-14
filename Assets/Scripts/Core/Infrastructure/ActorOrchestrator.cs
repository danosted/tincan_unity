using UnityEngine;
using TinCan.Core.Domain;
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

        public ActorOrchestrator(IActorRegistry actorRegistry, IInteractorRegistry interactorRegistry)
        {
            _actorRegistry = actorRegistry;
            _interactorRegistry = interactorRegistry;
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
        }
    }
}
