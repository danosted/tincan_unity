#nullable enable
using System.Collections.Generic;
using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Targeting;
using TinCan.Features.Abilities;
using TinCan.Features.Interaction;
using VContainer;

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
        private readonly Dictionary<IShipModule, IShipModuleRegistry> _moduleRegistries = new();
        private readonly ITargetableRegistry? _targetableRegistry;

        public ActorOrchestrator(IActorRegistry actorRegistry, IInteractorRegistry interactorRegistry, IAbilityRegistry abilityRegistry)
        {
            _actorRegistry = actorRegistry;
            _interactorRegistry = interactorRegistry;
            _abilityRegistry = abilityRegistry;
        }

        // The targetable registry belongs to the Targeting feature installer, which a profile can switch off, so it is
        // resolved optionally rather than required.
        [Inject]
        public ActorOrchestrator(IActorRegistry actorRegistry, IInteractorRegistry interactorRegistry, IAbilityRegistry abilityRegistry, IObjectResolver resolver)
            : this(actorRegistry, interactorRegistry, abilityRegistry)
        {
            _targetableRegistry = resolver.TryResolve<ITargetableRegistry>(out var targetables) ? targetables : null;
        }

        /// <summary>Test seam: an orchestrator that also registers targetables.</summary>
        public ActorOrchestrator(IActorRegistry actorRegistry, IInteractorRegistry interactorRegistry, IAbilityRegistry abilityRegistry, ITargetableRegistry targetableRegistry)
            : this(actorRegistry, interactorRegistry, abilityRegistry)
        {
            _targetableRegistry = targetableRegistry;
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

            // One controller per actor, or GAS ticks the actor twice.
            var abilityControllers = AbilityControllerSelection.OnePerActor(root.GetComponentsInChildren<IAbilityControllerBase>(true));
            foreach (var controller in abilityControllers)
            {
                _abilityRegistry.Register(controller);
            }

            if (_targetableRegistry != null)
            {
                foreach (var targetable in root.GetComponentsInChildren<ITargetable>(true)) _targetableRegistry.Register(targetable);
            }
        }

        public void UnregisterHierarchy(GameObject root)
        {
            // Unregister Identity
            if (root.TryGetComponent<IActor>(out var actor))
            {
                if (actor is IShipModule module) UnregisterShipModule(module);
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

            if (_targetableRegistry != null)
            {
                foreach (var targetable in root.GetComponentsInChildren<ITargetable>(true)) _targetableRegistry.Unregister(targetable);
            }
        }

        public void RegisterShipModule(IShipModule module, IShipModuleRegistry registry)
        {
            if (_moduleRegistries.TryGetValue(module, out var previous) && ReferenceEquals(previous, registry)) return;
            UnregisterShipModule(module);
            _moduleRegistries.Add(module, registry);
            registry.RegisterModule(module);
        }

        public void UnregisterShipModule(IShipModule module)
        {
            if (!_moduleRegistries.TryGetValue(module, out var registry)) return;
            _moduleRegistries.Remove(module);
            registry.UnregisterModule(module);
        }
    }
}
