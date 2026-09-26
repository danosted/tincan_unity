#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Features.Abilities;

namespace TinCan.Features.Items
{
    /// <summary>
    /// Turns "holding item X" into GAS state: applies the item's EquippedEffect and grants its abilities, and on the
    /// next change revokes exactly what it granted. Abilities the actor already had (starting abilities) are left
    /// alone, so an item never strips something it did not give.
    /// Ability grants are not replicated, so the equipment mediator calls this on the server and on the owning client
    /// (for prediction); proxies never do.
    /// </summary>
    public sealed class EquipmentAbilityBinder : IDisposable
    {
        private sealed class Grant
        {
            public ItemDefinition Item = null!;
            public ActiveGameplayEffect? Effect;
            public readonly List<AbilityDefinition> Abilities = new();
        }

        private readonly AbilitySystemUseCase _abilities;
        private readonly IActorRegistry _actors;
        private readonly Dictionary<Guid, Grant> _grants = new();

        public EquipmentAbilityBinder(AbilitySystemUseCase abilities, IActorRegistry actors)
        {
            _abilities = abilities;
            _actors = actors;
            _actors.OnActorUnregistered += Forget;
        }

        public void Dispose() => _actors.OnActorUnregistered -= Forget;

        /// <summary>The item whose grants are currently applied to this actor, if any.</summary>
        public ItemDefinition? BoundItem(Guid actorId) => _grants.TryGetValue(actorId, out var grant) ? grant.Item : null;

        /// <summary>Makes the actor's granted state match holding <paramref name="item"/> (null = empty hands).</summary>
        public void Bind(IAbilityControllerBase actor, ItemDefinition? item)
        {
            if (_grants.TryGetValue(actor.Id, out var current))
            {
                if (current.Item == item) return;
                Revoke(actor, current);
            }

            if (item == null) return;
            _grants[actor.Id] = Apply(actor, item);
        }

        private Grant Apply(IAbilityControllerBase actor, ItemDefinition item)
        {
            var grant = new Grant { Item = item };

            if (item.EquippedEffect != null) grant.Effect = _abilities.ApplyEffect(actor, item.EquippedEffect);

            foreach (var ability in item.GrantedAbilities)
            {
                if (ability == null || _abilities.HasAbility(actor, ability)) continue;
                _abilities.GrantAbility(actor, ability);
                grant.Abilities.Add(ability);
            }

            return grant;
        }

        private void Revoke(IAbilityControllerBase actor, Grant grant)
        {
            _grants.Remove(actor.Id);
            foreach (var ability in grant.Abilities) _abilities.RemoveAbility(actor, ability);
            if (grant.Effect != null) _abilities.RemoveEffect(actor, grant.Effect);
        }

        // The ability system drops a despawned actor's specs and effects itself; only our bookkeeping remains.
        private void Forget(IActor actor) => _grants.Remove(actor.Id);
    }
}
