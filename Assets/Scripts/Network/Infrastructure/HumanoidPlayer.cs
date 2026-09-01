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
        private NetworkTransformMediator _networkTransform = null!;
        private INetworkPlayerSpawner _spawner = null!;
        private uint _nextInputSequence;
        private uint _lastReconciledSequence;
        private Vector3 _pendingPositionCorrection = Vector3.zero;
        private readonly Dictionary<uint, Vector3> _predictedPositionsBySequence = new();
        private readonly Queue<uint> _predictedPositionHistory = new();

        [Header("Attributes (GAS)")]
        [SerializeField] private GameplayAttribute? _moveSpeedAttribute;
        [SerializeField] private GameplayAttribute? _jumpForceAttribute;
        [SerializeField] private GameplayAttribute? _staminaAttribute;
        [SerializeField] private GameplayAttribute? _healthAttribute;
        [SerializeField] private List<AbilityDefinition>? _startingAbilities;

        private readonly NetworkVariable<HumanoidInputState> _netInputState = new NetworkVariable<HumanoidInputState>(
            writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<PlayerAttachmentState> _attachmentState = new NetworkVariable<PlayerAttachmentState>(
            writePerm: NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<HumanoidMovementSnapshot> _movementSnapshot = new NetworkVariable<HumanoidMovementSnapshot>(
            writePerm: NetworkVariableWritePermission.Server);

        private const int MaxPredictedPositionSamples = 128;
        private const float ReconciliationSnapDistance = 3f;
        private const float ReconciliationSmoothing = 14f;
        private const float ReconciliationEpsilon = 0.0001f;

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
            _networkTransform = GetComponent<NetworkTransformMediator>();
            _nextInputSequence = _netInputState.Value.Sequence;

            // Register default attribute set wrapper for humanoids
            var attributes = new HumanoidAttributeSet(this, _moveSpeedAttribute, _jumpForceAttribute, _staminaAttribute, _healthAttribute);

            // Initialize base values for all clients and server to ensure prediction works instantly
            attributes.InitializeBaseValues(_movement.WalkSpeed, _movement.JumpForce, 100f, 100f);

            _abilitySync.RegisterAttributeSet(attributes);

            // Grant abilities directly through the mediator, which now correctly resolves the parent ID
            foreach (var ability in _startingAbilities ?? new List<AbilityDefinition>())
            {
                _abilitySync.GrantAbility(ability);
            }

            _netInputState.OnValueChanged += OnInputStateChanged;
            _movementSnapshot.OnValueChanged += OnMovementSnapshotChanged;

            if (IsOwner && !IsServer)
            {
                _networkTransform.enabled = false;
            }

            _spawner.NotifyPlayerSpawned(gameObject, OwnerClientId, IsOwner);
        }

        public override void OnNetworkDespawn()
        {
            _netInputState.OnValueChanged -= OnInputStateChanged;
            _movementSnapshot.OnValueChanged -= OnMovementSnapshotChanged;
            if (_networkTransform != null)
            {
                _networkTransform.enabled = true;
            }
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
                PublishMovementSnapshot();
                return;
            }

            if (IsOwner)
            {
                CapturePredictedPosition();
                ApplyOwnerReconciliation();
                return;
            }

            ApplyAttachmentPose();
        }

        private void CapturePredictedPosition()
        {
            uint sequence = _netInputState.Value.Sequence;
            if (sequence == 0) return;

            if (!_predictedPositionsBySequence.ContainsKey(sequence))
            {
                _predictedPositionHistory.Enqueue(sequence);
                while (_predictedPositionHistory.Count > MaxPredictedPositionSamples)
                {
                    uint oldest = _predictedPositionHistory.Dequeue();
                    _predictedPositionsBySequence.Remove(oldest);
                }
            }

            _predictedPositionsBySequence[sequence] = transform.position;
        }

        private void ApplyOwnerReconciliation()
        {
            if (_pendingPositionCorrection.sqrMagnitude <= ReconciliationEpsilon * ReconciliationEpsilon) return;

            float blend = HumanoidPredictionReconciliation.CalculateBlendFactor(ReconciliationSmoothing, Time.deltaTime);
            Vector3 correctionStep = _pendingPositionCorrection * blend;
            _pendingPositionCorrection -= correctionStep;

            if (_pendingPositionCorrection.sqrMagnitude <= ReconciliationEpsilon * ReconciliationEpsilon)
            {
                _pendingPositionCorrection = Vector3.zero;
            }

            _movement.SetPose(transform.position + correctionStep, transform.rotation);
        }

        private void OnMovementSnapshotChanged(HumanoidMovementSnapshot previous, HumanoidMovementSnapshot current)
        {
            if (!IsOwner || IsServer) return;
            if (current.LastProcessedInputSequence <= _lastReconciledSequence) return;
            if (!_predictedPositionsBySequence.TryGetValue(current.LastProcessedInputSequence, out Vector3 predictedPosition)) return;

            _lastReconciledSequence = current.LastProcessedInputSequence;
            PrunePredictedPositions(_lastReconciledSequence);

            if (!HumanoidPredictionReconciliation.TryComputePositionError(current.Position, predictedPosition, out Vector3 correction)) return;

            if (correction.magnitude >= ReconciliationSnapDistance)
            {
                _pendingPositionCorrection = Vector3.zero;
                _movement.SetPose(current.Position, current.Rotation);
                return;
            }

            _pendingPositionCorrection += correction;
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

        private void PublishMovementSnapshot()
        {
            var input = _netInputState.Value;
            _movementSnapshot.Value = new HumanoidMovementSnapshot
            {
                LastProcessedInputSequence = input.Sequence,
                Position = transform.position,
                Rotation = transform.rotation,
                HorizontalVelocity = Vector3.zero,
                VerticalVelocity = 0f,
                PreviousInputMask = input.ActiveInputMask
            };
        }

        private void PrunePredictedPositions(uint acknowledgedSequence)
        {
            while (_predictedPositionHistory.Count > 0 && _predictedPositionHistory.Peek() <= acknowledgedSequence)
            {
                uint sequence = _predictedPositionHistory.Dequeue();
                _predictedPositionsBySequence.Remove(sequence);
            }
        }

        public bool HasTag(GameplayTag tag) => _abilitySync.HasTag(tag);

        public void AddTag(GameplayTag tag) => _abilitySync.AddTag(tag);

        public void RemoveTag(GameplayTag tag) => _abilitySync.RemoveTag(tag);
        public HumanoidAttributeSet? GetAttributeSet() => _abilitySync.GetAttributeSet<HumanoidAttributeSet>();

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
