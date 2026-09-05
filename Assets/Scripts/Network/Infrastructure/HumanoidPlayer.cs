#nullable enable
using Unity.Netcode;
using UnityEngine;
using TinCan.Features.HumanoidMovement;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
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
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(HumanoidControllerView))]
    [RequireComponent(typeof(ThirdPersonLookView))]
    [RequireComponent(typeof(InteractorControllerView))]
    [RequireComponent(typeof(NetworkTransformMediator))]
    [RequireComponent(typeof(AbilityNetworkMediator))]
    public class HumanoidPlayer : NetworkMediator, IHumanoidCharacterView, TinCan.Features.Airship.IBuilder
    {
        public override bool IsSimulating => IsSpawned && (IsServer || IsOwner);

        [Header("Building / Crafting (Temporary)")]
        [SerializeField] private GameObject? _selectedModulePrefab;

        public GameObject? SelectedModulePrefab
        {
            get => _selectedModulePrefab;
            set => _selectedModulePrefab = value;
        }

        private HumanoidControllerView _movement = null!;
        private ThirdPersonLookView _look = null!;
        private AbilityNetworkMediator _abilitySync = null!;
        private INetworkPlayerSpawner _spawner = null!;
        private uint _nextInputSequence;

        [Header("Attributes (GAS)")]
        [SerializeField] private GameplayAttribute? _moveSpeedAttribute;
        [SerializeField] private GameplayAttribute? _jumpForceAttribute;
        [SerializeField] private GameplayAttribute? _staminaAttribute;
        [SerializeField] private HealthAttribute? _healthAttribute;
        [SerializeField] private MaxHealthAttribute? _maxHealthAttribute;
        [SerializeField] private List<AbilityDefinition>? _startingAbilities;

        private readonly NetworkVariable<HumanoidInputState> _netInputState = new NetworkVariable<HumanoidInputState>(
            writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<PlayerAttachmentState> _attachmentState = new NetworkVariable<PlayerAttachmentState>(
            writePerm: NetworkVariableWritePermission.Server);

        // IHumanoidCharacterView Implementation
        public IHumanoidMovementView Movement => _movement;
        public IOrbitalLookView Look => _look;
        public PlayerAttachmentState AttachmentState => _attachmentState.Value;

        public HumanoidInputState InputState
        {
            get => _netInputState.Value;
            set
            {
                if (!IsOwner) return;

                value.Sequence = ++_nextInputSequence;
                _netInputState.Value = value;
            }
        }

        [Inject]
        public void InjectPlayerSpawner(INetworkPlayerSpawner spawner)
        {
            _spawner = spawner;
        }

        public GameplayTagContainer ActiveTags => _abilitySync.ActiveTags;


        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _movement = GetComponent<HumanoidControllerView>();
            _look = GetComponent<ThirdPersonLookView>();
            _abilitySync = GetComponent<AbilityNetworkMediator>();

            // Register default attribute set wrapper for humanoids
            var attributes = new HumanoidAttributeSet(this, _moveSpeedAttribute, _jumpForceAttribute, _staminaAttribute);

            // Initialize base values for all clients and server to ensure prediction works instantly
            attributes.InitializeBaseValues(_movement.WalkSpeed, _movement.JumpForce, 100f);

            _abilitySync.RegisterAttributeSet(attributes);

            if (_healthAttribute && _maxHealthAttribute)
            {
                var health = new HealthAttributeSet(_abilitySync, _healthAttribute, _maxHealthAttribute);
                health.InitializeBaseValues(100f);
                _abilitySync.RegisterAttributeSet(health);
            }

            // Grant abilities directly through the mediator, which now correctly resolves the parent ID
            foreach (var ability in _startingAbilities ?? new List<AbilityDefinition>())
            {
                _abilitySync.GrantAbility(ability);
            }

            _netInputState.OnValueChanged += OnInputStateChanged;
            _spawner.NotifyPlayerSpawned(gameObject, OwnerClientId, IsOwner);
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

        private void LateUpdate()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                PublishAttachmentState();
                return;
            }

            ApplyAttachmentPose();
        }

        private void ApplyAttachmentPose()
        {
            if (IsOwner) return;

            var attachment = _attachmentState.Value;
            if (!attachment.IsAttached || !attachment.Platform.TryGet(out NetworkObject platform)) return;

            Transform platformTransform = platform.transform;
            transform.SetPositionAndRotation(
                platformTransform.TransformPoint(attachment.LocalPosition),
                platformTransform.rotation * attachment.LocalRotation);
        }

        private void PublishAttachmentState()
        {
            var platformTransform = _movement.CurrentGround.MovingGroundTransform;
            var platformObject = platformTransform != null
                ? platformTransform.GetComponentInParent<NetworkObject>()
                : null;

            _attachmentState.Value = platformObject != null && platformObject.IsSpawned
                ? new PlayerAttachmentState
                {
                    IsAttached = true,
                    Platform = new NetworkObjectReference(platformObject),
                    LocalPosition = platformObject.transform.InverseTransformPoint(transform.position),
                    LocalRotation = Quaternion.Inverse(platformObject.transform.rotation) * transform.rotation,
                    LastProcessedInputSequence = _netInputState.Value.Sequence
                }
                : new PlayerAttachmentState
                {
                    IsAttached = false,
                    LastProcessedInputSequence = _netInputState.Value.Sequence
                };
        }

        public bool HasTag(GameplayTag tag) => _abilitySync.HasTag(tag);

        public void AddTag(GameplayTag tag) => _abilitySync.AddTag(tag);

        public void RemoveTag(GameplayTag tag) => _abilitySync.RemoveTag(tag);
        public HumanoidAttributeSet? GetAttributeSet() => _abilitySync.GetAttributeSet<HumanoidAttributeSet>();

        public bool TryGetAttributeSet<TAttributeSet>(out TAttributeSet set) where TAttributeSet : class, IAttributeSet
            => _abilitySync.TryGetAttributeSet(out set);

        public bool TryGetAttribute(GameplayAttribute attribute, out AttributeValue value) => _abilitySync.TryGetAttribute(attribute, out value);
        public void SetAttribute(GameplayAttribute attribute, AttributeValue value) => _abilitySync.SetAttribute(attribute, value);
        public void ResetAttributesToBase() => _abilitySync.ResetAttributesToBase();

        public void GrantAbility(IAbilityDefinition definition) => _abilitySync.GrantAbility(definition);

        public void RemoveAbility(IAbilityDefinition definition) => _abilitySync.RemoveAbility(definition);

        public bool TryActivateAbility(IAbilityDefinition definition) => _abilitySync.TryActivateAbility(definition);

        public void HandleGameplayEvent(GameplayEventData eventData) => _abilitySync.HandleGameplayEvent(eventData);

        public bool TryActivateAbility(IAbilityDefinition definition, IAbilityControllerBase? target = null) => _abilitySync.TryActivateAbility(definition, target);
    }
}
