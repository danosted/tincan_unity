using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Events;
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
    public class AbilitySystemUseCase : ITickable, IInitializable, IDisposable
    {
        private readonly IAbilityRegistry _registry;
        private readonly IActorRegistry _actorRegistry;
        private readonly ITimeService _timeService;
        private readonly IEventPublisher _eventPublisher;

        // Internal tracking for specs and effects per actor
        private readonly Dictionary<Guid, List<AbilitySpec>> _actorAbilities = new();
        private readonly Dictionary<Guid, List<ActiveGameplayEffect>> _activeEffects = new();

        public AbilitySystemUseCase(IAbilityRegistry registry, IActorRegistry actorRegistry, ITimeService timeService, IEventPublisher eventPublisher)
        {
            _registry = registry;
            _actorRegistry = actorRegistry;
            _timeService = timeService;
            _eventPublisher = eventPublisher;
        }

        public void Initialize()
        {
            _actorRegistry.OnActorUnregistered += HandleActorUnregistered;
        }

        public void Dispose()
        {
            _actorRegistry.OnActorUnregistered -= HandleActorUnregistered;
        }

        // Actors never explicitly revoke granted abilities/effects today, so despawn is the only cleanup point.
        private void HandleActorUnregistered(IActor actor)
        {
            _actorAbilities.Remove(actor.Id);
            _activeEffects.Remove(actor.Id);
        }

        public void Tick()
        {
            float currentTime = _timeService.Time;

            foreach (var actor in _registry.AllControllers)
            {
                // Only tick globally if this actor is NOT explicitly predicted by a movement loop
                if (actor is ISimulatedActor) continue;

                // Also, only tick global effects if the actor itself is considered "simulating" (locally owned or server authority)
                if (!actor.IsSimulating) continue;

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

                    // One arm per InputPolicy; guards express exactly when that policy triggers.
                    switch (spec.Definition.InputPolicy)
                    {
                        case AbilityInputPolicy.OnInputTriggered when justPressed && !spec.IsActive:
                        case AbilityInputPolicy.OnInputReleased when justReleased && !spec.IsActive:
                        case AbilityInputPolicy.OnInputHeld when isPressedNow && !spec.IsActive:
                            TryActivateAbility(actor, spec.Definition);
                            break;
                        case AbilityInputPolicy.OnInputHeld when !isPressedNow && spec.IsActive:
                            EndAbility(actor, spec);
                            break;
                    }
                }
            }
        }

        public void EndAbility(IAbilityControllerBase actor, AbilitySpec spec)
        {
            if (!spec.IsActive) return;
            spec.IsActive = false;

            // Remove the active buff when the ability stops (from whoever received it)
            if (spec.AppliedActiveEffect != null && spec.EffectRecipient != null)
            {
                RemoveEffect(spec.EffectRecipient, spec.AppliedActiveEffect);
                spec.AppliedActiveEffect = null;
                spec.EffectRecipient = null;
            }

            // Remove any tags added by this ability's timing windows
            foreach (var tag in spec.ActiveWindowTags)
            {
                actor.RemoveTag(tag);
            }
            spec.ActiveWindowTags.Clear();

            _eventPublisher.Publish(new AbilityEndedEvent(actor.Id, spec.Definition.name));
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

                // One arm per (shouldHaveTag, hasTag) combo; the other two combos are no-ops.
                switch (shouldHaveTag, hasTag)
                {
                    case (true, false):
                        actor.AddTag(window.Tag);
                        spec.ActiveWindowTags.Add(window.Tag);
                        break;
                    case (false, true):
                        actor.RemoveTag(window.Tag);
                        spec.ActiveWindowTags.Remove(window.Tag);
                        break;
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

        public void RemoveAbility(IAbilityControllerBase actor, AbilityDefinition definition)
        {
            if (!_actorAbilities.TryGetValue(actor.Id, out var abilities)) return;

            var spec = abilities.FirstOrDefault(a => a.Definition == definition);
            if (spec == null) return;

            if (spec.IsActive) EndAbility(actor, spec);
            abilities.Remove(spec);
        }

        public void CancelAbility(IAbilityControllerBase actor, AbilityDefinition definition)
        {
            if (!_actorAbilities.TryGetValue(actor.Id, out var abilities)) return;

            var spec = abilities.FirstOrDefault(a => a.Definition == definition);
            if (spec is { IsActive: true })
            {
                EndAbility(actor, spec);
            }
        }

        public bool TryActivateAbility(IAbilityControllerBase actor, AbilityDefinition definition, IAbilityControllerBase target = null)
        {
            if (!_actorAbilities.TryGetValue(actor.Id, out var abilities)) return false;

            var ability = abilities.FirstOrDefault(a => a.Definition == definition);
            if (ability == null) return false;

            // One arm per (IsActive, IsToggleable) combo; costs would be a future arm here too.
            switch (isActive: ability.IsActive, isToggleable: definition.IsToggleable)
            {
                case (isActive: true, isToggleable: true):
                    CancelAbility(actor, definition);
                    return true;
                case (isActive: true, isToggleable: false):
                    return false;
                case (isActive: false, isToggleable: _) when !CanActivateAbility(actor, ability, target):
                    return false;
                default:
                    ExecuteAbility(actor, ability, target);
                    return true;
            }
        }

        /// <summary>
        /// Public entry point to apply a gameplay effect to an actor.
        /// Returns a result object for easier integration with Visual Scripting.
        /// </summary>
        public GameplayEffectResult ApplyGameplayEffect(IAbilityControllerBase actor, GameplayEffectDefinition definition)
        {
            if (actor == null) return GameplayEffectResult.Failure("Target actor is null.");
            if (definition == null) return GameplayEffectResult.Failure("Effect definition is null.");

            ApplyEffect(actor, definition);
            return GameplayEffectResult.Successful();
        }

        private bool CanActivateAbility(IAbilityControllerBase actor, AbilitySpec spec, IAbilityControllerBase target = null)
        {
            var actorActiveTags = actor.ActiveTags;
            var def = spec.Definition;

            // Blocked by tags?
            if (def.ActivationBlockedTagsOnActor.Any(t => actorActiveTags.HasTag(t))) return false;

            // Missing required tags?
            if (def.ActivationRequiredTagsOnActor.Any(t => !actorActiveTags.HasTag(t))) return false;

            // Blocked by target tags?
            if (def.ActivationBlockedTagsOnTarget.Any(t => target != null && target.ActiveTags.HasTag(t))) return false;

            // Missing required target tags?
            if (def.ActivationRequiredTagsOnTarget.Any(t => target != null && !target.ActiveTags.HasTag(t))) return false;

            // Cooldown?
            if (spec.IsOnCooldown(_timeService.Time)) return false;

            return true;
        }

        private void ExecuteAbility(IAbilityControllerBase actor, AbilitySpec spec, IAbilityControllerBase target = null)
        {
            spec.Activate(_timeService.Time);
            _eventPublisher.Publish(new AbilityActivatedEvent(actor.Id, spec.Definition.name));

            // Apply the active buff to the correct target
            if (spec.Definition.ActiveEffect != null)
            {
                var effectRecipient = spec.Definition.ActiveEffectTarget == EffectTarget.ProvidedTarget ? target : actor;

                if (effectRecipient != null)
                {
                    spec.AppliedActiveEffect = ApplyEffect(effectRecipient, spec.Definition.ActiveEffect);
                    spec.EffectRecipient = effectRecipient;
                }
            }

            // Apply Cooldown Effect to the activator
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
            if (!_actorAbilities.TryGetValue(eventData.Target.Id, out var abilities)) return;

            // Any ability whose TriggerTag matches this event's tag activates.
            foreach (var spec in abilities)
            {
                if (spec.Definition.TriggerTag != null && spec.Definition.TriggerTag == eventData.EventTag)
                {
                    TryActivateAbility(eventData.Target, spec.Definition);
                }
            }
        }

        private void RemoveEffect(IAbilityControllerBase actor, ActiveGameplayEffect effect)
        {
            if (!_activeEffects.TryGetValue(actor.Id, out var effects)) return;
            effects.Remove(effect);

            var grantedEffectTags = effects.SelectMany(e => e.Definition.GrantedTags).ToList();

            // Remove Tags
            foreach (var tag in effect.Definition.GrantedTags)
            {
                // Note: Only remove if no other active effect grants this tag
                if (grantedEffectTags.Contains(tag)) continue;
                actor.RemoveTag(tag);
            }

            UpdateAttributes(actor);

            // If this effect was the primary active effect for an ability, end the ability automatically.
            if (!_actorAbilities.TryGetValue(actor.Id, out var abilities)) return;

            var parentSpec = abilities.FirstOrDefault(s => s.AppliedActiveEffect == effect);
            if (parentSpec is { IsActive: true })
            {
                EndAbility(actor, parentSpec);
            }
        }

        private void ExecuteInstantEffect(IAbilityControllerBase actor, GameplayEffectDefinition definition)
        {
            foreach (var modifier in definition.Modifiers)
            {
                if (modifier.Attribute == null) continue;

                if (actor.TryGetAttribute(modifier.Attribute, out var attrVal))
                {
                    float oldBase = attrVal.BaseValue;

                    // Instant effects modify the BASE value permanently
                    attrVal.BaseValue = modifier.Operation switch
                    {
                        ModifierOp.Add => attrVal.BaseValue + modifier.Value,
                        ModifierOp.Multiply => attrVal.BaseValue * modifier.Value,
                        ModifierOp.Override => modifier.Value,
                        _ => attrVal.BaseValue
                    };

                    if (modifier.ClampMaxAttribute != null && actor.TryGetAttribute(modifier.ClampMaxAttribute, out var maxVal))
                    {
                        attrVal.BaseValue = Mathf.Clamp(attrVal.BaseValue, 0f, Mathf.Max(0f, maxVal.CurrentValue));
                    }

                    actor.SetAttribute(modifier.Attribute, attrVal);
                    _eventPublisher.LogInfo("AbilitySystem", $"Instant Effect {definition.name} modified Base {modifier.Attribute.name}: {oldBase} -> {attrVal.BaseValue}");
                }
            }

            // Recalculate current values based on the new base and existing duration effects
            UpdateAttributes(actor);
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
                        _eventPublisher.LogWarning("AbilitySystem", $"Modifier on {effect.Definition.name} has a null Attribute reference!");
                        continue;
                    }

                    if (actor.TryGetAttribute(modifier.Attribute, out var attrVal))
                    {
                        float oldVal = attrVal.CurrentValue;
                        ApplyModifier(ref attrVal, modifier);
                        actor.SetAttribute(modifier.Attribute, attrVal);
                        _eventPublisher.LogInfo("AbilitySystem", $"Modified {modifier.Attribute.name}: {oldVal} -> {attrVal.CurrentValue}");
                    }
                    else
                    {
                        _eventPublisher.LogWarning("AbilitySystem", $"Actor {actor.Id} missing attribute {modifier.Attribute.name}!");
                    }
                }
            }
        }

        private void ApplyModifier(ref AttributeValue attr, AttributeModifier mod)
        {
            attr.CurrentValue = mod.Operation switch
            {
                ModifierOp.Add => attr.CurrentValue + mod.Value,
                ModifierOp.Multiply => attr.CurrentValue * mod.Value,
                ModifierOp.Override => mod.Value,
                _ => attr.CurrentValue
            };
        }
    }
}
