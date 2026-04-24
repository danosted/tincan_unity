using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Abilities.Attributes;
using VContainer.Unity;
using UnityEngine;

using TinCan.Features.HumanoidMovement;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Application Layer: Logic processor for the Gameplay Ability System.
    /// Handles ticking of effects, cooldowns, and ability activation logic.
    /// Supports Input-Driven Simulation for predicted gameplay.
    /// </summary>
    public class AbilitySystemUseCase : ITickable
    {
        private readonly IActorRegistry _registry;
        private readonly ITimeService _timeService;

        // Internal tracking for specs and effects per actor
        private readonly Dictionary<Guid, List<AbilitySpec>> _actorAbilities = new();
        private readonly Dictionary<Guid, List<ActiveGameplayEffect>> _activeEffects = new();

        public AbilitySystemUseCase(IActorRegistry registry, ITimeService timeService)
        {
            _registry = registry;
            _timeService = timeService;
        }

        public void Tick()
        {
            float currentTime = _timeService.Time;

            foreach (var actor in _registry.GetActors<IAbilityControllerBase>())
            {
                // Global effects and non-predicted actors still tick here
                if (!actor.IsSimulating) continue;
                if (actor is IHumanoidCharacterView) continue; // Handled by ProcessAbilitySimulation

                UpdateEffects(actor, currentTime);
                UpdateAbilities(actor, currentTime);
            }
        }

        /// <summary>
        /// Authoritative simulation tick for a specific actor.
        /// Called by movement systems to ensure predicted abilities are synced with movement.
        /// </summary>
        public void ProcessAbilitySimulation(IAbilityControllerBase actor, HumanoidInputState input, ulong previousInputMask, float deltaTime)
        {
            float currentTime = _timeService.Time;

            // 1. Update passive state (Effects & Active Ability Windows)
            UpdateEffects(actor, currentTime);
            UpdateAbilities(actor, currentTime);

            // 2. Process Input Triggers
            if (!_actorAbilities.TryGetValue(actor.Id, out var abilities)) return;

            ulong currentMask = input.ActiveInputMask;

            foreach (var spec in abilities)
            {
                var trigger = spec.Definition.TriggerInput;
                if (trigger != null && trigger.BitIndex >= 0)
                {
                    bool isPressedNow = (currentMask & (1UL << trigger.BitIndex)) != 0;
                    bool wasPressedBefore = (previousInputMask & (1UL << trigger.BitIndex)) != 0;

                    bool justPressed = isPressedNow && !wasPressedBefore;
                    bool justReleased = !isPressedNow && wasPressedBefore;

                    if (spec.Definition.InputPolicy == AbilityInputPolicy.OnInputTriggered)
                    {
                        if (justPressed && !spec.IsActive) TryActivateAbility(actor, spec.Definition);
                    }
                    else if (spec.Definition.InputPolicy == AbilityInputPolicy.OnInputHeld)
                    {
                        if (isPressedNow && !spec.IsActive) TryActivateAbility(actor, spec.Definition);
                        else if (!isPressedNow && spec.IsActive) EndAbility(actor, spec);
                    }
                    else if (spec.Definition.InputPolicy == AbilityInputPolicy.OnInputReleased)
                    {
                        if (justReleased && !spec.IsActive) TryActivateAbility(actor, spec.Definition);
                    }
                }
            }
        }

        public void EndAbility(IAbilityControllerBase actor, AbilitySpec spec)
        {
            if (!spec.IsActive) return;
            spec.IsActive = false;

            // Remove the active buff when the ability stops (e.g., player releases Shift)
            if (spec.AppliedActiveEffect != null)
            {
                RemoveEffect(actor, spec.AppliedActiveEffect);
                spec.AppliedActiveEffect = null;
            }

            // Remove any tags added by this ability's timing windows
            foreach (var tag in spec.ActiveWindowTags)
            {
                actor.RemoveTag(tag);
            }
            spec.ActiveWindowTags.Clear();

            Debug.Log($"[AbilitySystem] Actor {actor.Id} ended {spec.Definition.name}");
        }

        private void UpdateAbilities(IAbilityControllerBase actor, float currentTime)
        {
            if (!_actorAbilities.TryGetValue(actor.Id, out var abilities)) return;

            foreach (var spec in abilities)
            {
                if (!spec.IsActive) continue;

                UpdateTimingWindows(actor, spec, currentTime);
            }
        }

        private void UpdateTimingWindows(IAbilityControllerBase actor, AbilitySpec spec, float currentTime)
        {
            float elapsed = currentTime - spec.StartTime;
            var def = spec.Definition;

            foreach (var window in def.TimingTagWindows)
            {
                bool shouldHaveTag = elapsed >= window.StartOffset && elapsed <= (window.StartOffset + window.Duration);
                bool hasTag = spec.ActiveWindowTags.Contains(window.Tag);

                if (shouldHaveTag && !hasTag)
                {
                    actor.AddTag(window.Tag);
                    spec.ActiveWindowTags.Add(window.Tag);
                }
                else if (!shouldHaveTag && hasTag)
                {
                    actor.RemoveTag(window.Tag);
                    spec.ActiveWindowTags.Remove(window.Tag);
                }
            }

            // End ability if all windows passed?
            // For now, let's assume abilities have a fixed duration or manual end.
        }

        private void UpdateEffects(IAbilityControllerBase actor, float currentTime)
        {
            if (!_activeEffects.TryGetValue(actor.Id, out var effects)) return;

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];
                if (effect.IsExpired(currentTime))
                {
                    RemoveEffect(actor, effect);
                }
            }
        }

        public void GrantAbility(IAbilityControllerBase actor, AbilityDefinition definition)
        {
            if (!_actorAbilities.TryGetValue(actor.Id, out var abilities))
            {
                abilities = new List<AbilitySpec>();
                _actorAbilities[actor.Id] = abilities;
            }

            if (abilities.Any(a => a.Definition == definition)) return;
            abilities.Add(new AbilitySpec(definition));
        }

        public bool TryActivateAbility(IAbilityControllerBase actor, AbilityDefinition definition)
        {
            if (!_actorAbilities.TryGetValue(actor.Id, out var abilities)) return false;

            var spec = abilities.FirstOrDefault(a => a.Definition == definition);
            if (spec == null) return false;

            // 1. Check Tags
            if (!CanActivateAbility(actor, spec)) return false;

            // 2. Check Costs (Future implementation)

            // 3. Activate
            ExecuteAbility(actor, spec);
            return true;
        }

        private bool CanActivateAbility(IAbilityControllerBase actor, AbilitySpec spec)
        {
            var tags = actor.ActiveTags;
            var def = spec.Definition;

            // Blocked by tags?
            if (def.ActivationBlockedTags.Any(t => tags.HasTag(t))) return false;

            // Missing required tags?
            if (def.ActivationRequiredTags.Any(t => !tags.HasTag(t))) return false;

            // Cooldown?
            if (spec.IsOnCooldown(_timeService.Time)) return false;

            return true;
        }

        private void ExecuteAbility(IAbilityControllerBase actor, AbilitySpec spec)
        {
            spec.Activate(_timeService.Time);
            Debug.Log($"[AbilitySystem] Actor {actor.Id} activated {spec.Definition.name}");

            // Apply the active buff (e.g., the 1.5x speed boost)
            if (spec.Definition.ActiveEffect != null)
            {
                spec.AppliedActiveEffect = ApplyEffect(actor, spec.Definition.ActiveEffect);
            }

            // Apply Cooldown Effect if any
            if (spec.Definition.CooldownEffect != null)
            {
                ApplyEffect(actor, spec.Definition.CooldownEffect);
            }
        }

        public ActiveGameplayEffect ApplyEffect(IAbilityControllerBase actor, GameplayEffectDefinition definition)
        {
            var effect = new ActiveGameplayEffect(definition, _timeService.Time);

            if (definition.DurationType == DurationType.Instant)
            {
                ExecuteInstantEffect(actor, definition);
                return effect;
            }

            if (!_activeEffects.TryGetValue(actor.Id, out var effects))
            {
                effects = new List<ActiveGameplayEffect>();
                _activeEffects[actor.Id] = effects;
            }

            effects.Add(effect);

            // Grant Tags
            foreach (var tag in definition.GrantedTags)
            {
                actor.AddTag(tag);
            }

            // Apply Attribute Modifiers
            UpdateAttributes(actor);

            return effect;
        }

        public void SendGameplayEvent(GameplayEventData eventData)
        {
            if (eventData.Target == null) return;

            eventData.Target.HandleGameplayEvent(eventData);

            // Check if any ability triggers on this event
            if (_actorAbilities.TryGetValue(eventData.Target.Id, out var abilities))
            {
                foreach (var spec in abilities)
                {
                    if (spec.Definition.TriggerTag != null && spec.Definition.TriggerTag == eventData.EventTag)
                    {
                        TryActivateAbility(eventData.Target, spec.Definition);
                    }
                }
            }
        }

        private void RemoveEffect(IAbilityControllerBase actor, ActiveGameplayEffect effect)
        {
            if (!_activeEffects.TryGetValue(actor.Id, out var effects)) return;
            effects.Remove(effect);

            // Remove Tags
            foreach (var tag in effect.Definition.GrantedTags)
            {
                // Note: Only remove if no other active effect grants this tag (simplified for now)
                actor.RemoveTag(tag);
            }

            UpdateAttributes(actor);
        }

        private void ExecuteInstantEffect(IAbilityControllerBase actor, GameplayEffectDefinition definition)
        {
            // Apply modifiers once and forget
            // UpdateAttributes(actor); // Instant effects need special handling for non-permanent changes
        }

        private void UpdateAttributes(IAbilityControllerBase actor)
        {
            // Reset to base
            actor.ResetAttributesToBase();

            if (!_activeEffects.TryGetValue(actor.Id, out var effects)) return;

            foreach (var effect in effects)
            {
                foreach (var modifier in effect.Definition.Modifiers)
                {
                    if (modifier.Attribute == null)
                    {
                        Debug.LogWarning($"[AbilitySystem] Modifier on {effect.Definition.name} has a null Attribute reference!");
                        continue;
                    }

                    if (actor.TryGetAttribute(modifier.Attribute, out var attrVal))
                    {
                        float oldVal = attrVal.CurrentValue;
                        ApplyModifier(ref attrVal, modifier);
                        actor.SetAttribute(modifier.Attribute, attrVal);
                        Debug.Log($"[AbilitySystem] Modified {modifier.Attribute.name}: {oldVal} -> {attrVal.CurrentValue}");
                    }
                    else
                    {
                        Debug.LogWarning($"[AbilitySystem] Actor {actor.Id} missing attribute {modifier.Attribute.name}!");
                    }
                }
            }
        }

        private void ApplyModifier(ref AttributeValue attr, AttributeModifier mod)
        {
            switch (mod.Operation)
            {
                case ModifierOp.Add:
                    attr.CurrentValue += mod.Value;
                    break;
                case ModifierOp.Multiply:
                    attr.CurrentValue *= mod.Value;
                    break;
                case ModifierOp.Override:
                    attr.CurrentValue = mod.Value;
                    break;
            }
        }
    }
}
