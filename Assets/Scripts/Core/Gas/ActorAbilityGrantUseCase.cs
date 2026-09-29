#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Core.Gas
{
    /// <summary>
    /// Application Layer: grants each humanoid and airship the abilities the loaded features contribute as
    /// <see cref="ActorAbilityGrant"/>s, as the actor registers. Runs on every peer, like the prefab's own starting
    /// abilities, so owners can predict them. The actors know nothing about features; this is their ability socket.
    /// </summary>
    public sealed class ActorAbilityGrantUseCase : IInitializable, IDisposable
    {
        private readonly IActorRegistry _actors;
        private readonly Dictionary<ActorKind, List<AbilityDefinition>> _grants;

        public ActorAbilityGrantUseCase(IActorRegistry actors, FeatureInstallerCatalog features)
        {
            _actors = actors;
            _grants = features.Installers
                .OfType<FeatureInstaller.IExtension<ActorAbilityGrant>>()
                .SelectMany(e => e.Contributions)
                .Where(g => g != null && g.Ability != null)
                .GroupBy(g => g.Actor)
                .ToDictionary(g => g.Key, g => g.Select(grant => grant.Ability!).Distinct().ToList());
        }

        /// <summary>The abilities loaded features grant to an actor of <paramref name="kind"/>, in installer order.</summary>
        public IReadOnlyList<AbilityDefinition> For(ActorKind kind) =>
            _grants.TryGetValue(kind, out var abilities) ? abilities : Array.Empty<AbilityDefinition>();

        public void Initialize()
        {
            if (_grants.Count == 0) return;

            _actors.OnActorRegistered += GrantTo;
            foreach (var actor in _actors.AllActors.ToList()) GrantTo(actor);
        }

        public void Dispose() => _actors.OnActorRegistered -= GrantTo;

        public void GrantTo(IActor actor)
        {
            if (KindOf(actor) is not { } kind || ControllerOf(actor) is not { } controller) return;

            foreach (var ability in For(kind)) controller.GrantAbility(ability);
        }

        private static ActorKind? KindOf(IActor actor) => actor switch
        {
            IHumanoidActor => ActorKind.Humanoid,
            IShipActor => ActorKind.Airship,
            _ => null,
        };

        // A humanoid is its own controller; an airship's controller is a sibling component on its root.
        private static IAbilityControllerBase? ControllerOf(IActor actor) =>
            actor as IAbilityControllerBase ?? (actor is Component component ? component.GetComponent<IAbilityControllerBase>() : null);
    }
}
