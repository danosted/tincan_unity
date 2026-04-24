using Unity.Netcode;
using UnityEngine;
using TinCan.Features.HumanoidMovement;
using TinCan.Core.Domain;
using TinCan.Features.Possession;
using TinCan.Features.Interaction;
using System;
using VContainer;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Abilities;
using TinCan.Features.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Network.Infrastructure.Abilities;
using System.Collections.Generic;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Mediator that wraps a complete Humanoid character to provide networking capabilities.
    /// Bridges the local domain logic with the network state at the "Face" level.
    /// </summary>
    [RequireComponent(typeof(HumanoidControllerView))]
    [RequireComponent(typeof(ThirdPersonLookView))]
    [RequireComponent(typeof(InteractorControllerView))]
    [RequireComponent(typeof(NetworkTransformMediator))]
    [RequireComponent(typeof(AbilityNetworkMediator))]
    public class HumanoidPlayer : NetworkMediator, IHumanoidCharacterView
    {
        private HumanoidControllerView _movement;
        private ThirdPersonLookView _look;
        private NetworkTransformMediator _transformSync;
        private AbilityNetworkMediator _abilitySync;

        [Header("Attributes (GAS)")]
        [SerializeField] private GameplayAttribute _moveSpeedAttribute;
        [SerializeField] private GameplayAttribute _jumpForceAttribute;
        [SerializeField] private GameplayAttribute _staminaAttribute;
        [SerializeField] private List<AbilityDefinition> _startingAbilities;

        private readonly NetworkVariable<HumanoidInputState> _netInputState = new NetworkVariable<HumanoidInputState>(
            writePerm: NetworkVariableWritePermission.Owner);

        // IHumanoidCharacterView Implementation
        public IHumanoidMovementView Movement => _movement;
        public IOrbitalLookView Look => _look;

        public HumanoidInputState InputState
        {
            get => _netInputState.Value;
            set
            {
                if (IsOwner) _netInputState.Value = value;
            }
        }

        public GameplayTagContainer ActiveTags => _abilitySync.ActiveTags;


        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _movement = GetComponent<HumanoidControllerView>();
            _look = GetComponent<ThirdPersonLookView>();
            _transformSync = GetComponent<NetworkTransformMediator>();
            _abilitySync = GetComponent<AbilityNetworkMediator>();

            // Register default attribute set wrapper for humanoids
            var attributes = new HumanoidAttributeSet(this, _moveSpeedAttribute, _jumpForceAttribute, _staminaAttribute);

            // Initialize base values for all clients and server to ensure prediction works instantly
            attributes.InitializeBaseValues(_movement.WalkSpeed, _movement.JumpForce, 100f);

            _abilitySync.RegisterAttributeSet(attributes);

            // Grant abilities directly through the mediator, which now correctly resolves the parent ID
            foreach (var ability in _startingAbilities)
            {
                _abilitySync.GrantAbility(ability);
            }

            _netInputState.OnValueChanged += OnInputStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            _netInputState.OnValueChanged -= OnInputStateChanged;
            base.OnNetworkDespawn();
        }

        private void OnInputStateChanged(HumanoidInputState previous, HumanoidInputState current)
        {
            if (!IsOwner)
            {
                InputState = current;
            }
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                // Sync Input State for remote simulation
                _netInputState.Value = InputState;

                // Update platform for transform sync
                _transformSync.SetPlatform(_movement.CurrentGround.GroundTransform);
            }
        }

        public bool HasTag(GameplayTag tag) => _abilitySync.HasTag(tag);

        public void AddTag(GameplayTag tag) => _abilitySync.AddTag(tag);

        public void RemoveTag(GameplayTag tag) => _abilitySync.RemoveTag(tag);
        T IAbilityController.GetAttributeSet<T>() => _abilitySync.GetAttributeSet<T>();

        public bool TryGetAttribute(GameplayAttribute attribute, out AttributeValue value) => _abilitySync.TryGetAttribute(attribute, out value);
        public void SetAttribute(GameplayAttribute attribute, AttributeValue value) => _abilitySync.SetAttribute(attribute, value);
        public void ResetAttributesToBase() => _abilitySync.ResetAttributesToBase();

        public void GrantAbility(AbilityDefinition definition) => _abilitySync.GrantAbility(definition);

        public void RemoveAbility(AbilityDefinition definition) => _abilitySync.RemoveAbility(definition);

        public bool TryActivateAbility(AbilityDefinition definition) => _abilitySync.TryActivateAbility(definition);

        public void HandleGameplayEvent(GameplayEventData eventData) => _abilitySync.HandleGameplayEvent(eventData);

    }
}
