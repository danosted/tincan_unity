#nullable enable
using System.Collections.Generic;
using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Domain.Targeting;
using TinCan.Features.Abilities;
using TinCan.Features.Entities;
using TinCan.Features.Interaction;
using VContainer;

namespace TinCan.Core.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: registers each entity's actors and capabilities in the domain registries, once per
    /// entity. Entities (EntityNetworkMediator) are the only callers.
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

        // The targetable registry is a core service (TinCan.Targeting's installer always loads). The constructor above,
        // without it, is a test seam for tests that don't care about targeting.
        [Inject]
        public ActorOrchestrator(IActorRegistry actorRegistry, IInteractorRegistry interactorRegistry, IAbilityRegistry abilityRegistry, ITargetableRegistry targetableRegistry)
            : this(actorRegistry, interactorRegistry, abilityRegistry)
        {
            _targetableRegistry = targetableRegistry;
        }

        public IEntityRegistry Entities { get; } = new EntityRegistry();

        public void RegisterEntity(IEntity entity)
        {
            if (!Entities.Register(entity)) return;
            var root = entity.Root;

            // Identity: several root components share the entity's id; the registry holds one of them.
            var actor = PrimaryActorSelection.Choose(root.GetComponents<IActor>());
            if (actor != null) _actorRegistry.Register(actor);

            foreach (var interactor in Owned<IInteractorView>(entity))
            {
                _interactorRegistry.Register(interactor);
            }

            // One controller per actor, or GAS ticks the actor twice.
            foreach (var controller in AbilityControllerSelection.OnePerActor(Owned<IAbilityControllerBase>(entity)))
            {
                _abilityRegistry.Register(controller);
            }

            if (_targetableRegistry != null)
            {
                foreach (var targetable in Owned<ITargetable>(entity)) _targetableRegistry.Register(targetable);
            }
        }

        public void UnregisterEntity(IEntity entity)
        {
            if (!Entities.Unregister(entity)) return;
            var root = entity.Root;

            var actor = PrimaryActorSelection.Choose(root.GetComponents<IActor>());
            if (actor != null)
            {
                if (actor is IShipModule module) UnregisterShipModule(module);
                _actorRegistry.Unregister(actor);
            }

            foreach (var interactor in Owned<IInteractorView>(entity))
            {
                _interactorRegistry.Unregister(interactor);
            }

            foreach (var controller in Owned<IAbilityControllerBase>(entity))
            {
                _abilityRegistry.Unregister(controller);
            }

            if (_targetableRegistry != null)
            {
                foreach (var targetable in Owned<ITargetable>(entity)) _targetableRegistry.Unregister(targetable);
            }
        }

        // An entity's capabilities are the components whose nearest entity is itself: a fixture parented under the ship
        // is its own entity and registers its own.
        private static IEnumerable<T> Owned<T>(IEntity entity)
        {
            foreach (var capability in entity.Root.GetComponentsInChildren<T>(true))
            {
                if (capability is Component component && ReferenceEquals(component.GetComponentInParent<IEntity>(true), entity)) yield return capability;
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
