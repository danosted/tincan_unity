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
    public class HumanoidPlayer : NetworkMediator, IHumanoidCharacterView, IBufferedInputSource, IPredictedHumanoid, TinCan.Features.Airship.IBuilder
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

        // Each send repeats the last few inputs, so any RedundantInputs - 1 consecutive lost packets lose nothing.
        private const int RedundantInputs = 4;

        private readonly NetworkVariable<PlayerAttachmentState> _attachmentState = new NetworkVariable<PlayerAttachmentState>(
            writePerm: NetworkVariableWritePermission.Server);
        private readonly HumanoidInputBuffer _inputBuffer = new();
        private readonly Queue<HumanoidInputState> _recentInputs = new();
        private HumanoidInputState _localInput;
        private HumanoidInputState _serverInput;

        // IHumanoidCharacterView Implementation
        public IHumanoidMovementView Movement => _movement;
        public IOrbitalLookView Look => _look;
        public PlayerAttachmentState AttachmentState => _attachmentState.Value;
        public HumanoidInputBufferStats InputBufferStats => _inputBuffer.Stats;
        private uint LastProcessedSequence => IsOwner ? _localInput.Sequence : _inputBuffer.LastConsumedSequence;

        /// <summary>
        /// Owner: the input gathered this tick, stamped with a sequence and streamed to the server.
        /// Server (remote owner): the input consumed from the buffer for this tick. Proxies do not simulate.
        /// </summary>
        public HumanoidInputState InputState
        {
            get => IsOwner ? _localInput : _serverInput;
            set
            {
                if (!IsOwner) return;

                value.Sequence = ++_nextInputSequence;
                _localInput = value;
                if (!IsServer) SendInput(value);
            }
        }

        public void AdvanceInput()
        {
            if (!IsSpawned || !IsServer || IsOwner) return;

            _serverInput = _inputBuffer.Consume();
        }

        private void SendInput(HumanoidInputState input)
        {
            if (!IsSpawned) return;

            _recentInputs.Enqueue(input);
            while (_recentInputs.Count > RedundantInputs) _recentInputs.Dequeue();
            _inputSendTimes[input.Sequence] = Time.realtimeSinceStartup;
            SubmitInputsServerRpc(_recentInputs.ToArray());
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void SubmitInputsServerRpc(HumanoidInputState[] inputs, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            _inputBuffer.Receive(inputs);
        }

        // IPredictedHumanoid: the owner predicts; the server reports what it did with each input.
        private readonly Dictionary<uint, float> _inputSendTimes = new();
        private HumanoidAuthoritativeState _pendingServerState;
        private bool _hasPendingServerState;
        private uint _newestServerSequence;

        public bool IsLocallyPredicted => IsSpawned && IsOwner && !IsServer;
        public bool PublishesAuthoritativeState => IsSpawned && IsServer && !IsOwner;
        public HumanoidPredictionStats PredictionStats { get; } = new();

        public bool TryTakeAuthoritativeState(out HumanoidAuthoritativeState state)
        {
            state = _pendingServerState;
            if (!_hasPendingServerState) return false;

            _hasPendingServerState = false;
            return true;
        }

        public void PublishAuthoritativeState(in HumanoidAuthoritativeState state)
        {
            if (!PublishesAuthoritativeState) return;

            // Express the state relative to the platform's NetworkObject, which the owner can resolve.
            var platformObject = state.Platform != null ? state.Platform.GetComponentInParent<NetworkObject>() : null;
            bool hasPlatform = platformObject != null && platformObject.IsSpawned;
            var wire = HumanoidAuthoritativeState.FromWorld(
                state.Sequence, state.TeleportEpoch, hasPlatform ? platformObject!.transform : null,
                state.WorldPosition, state.WorldHorizontalVelocity, state.VerticalVelocity);

            ReceiveAuthoritativeStateClientRpc(new HumanoidMovementSnapshot
            {
                Sequence = wire.Sequence,
                TeleportEpoch = wire.TeleportEpoch,
                HasPlatform = hasPlatform,
                Platform = hasPlatform ? new NetworkObjectReference(platformObject!) : default,
                LocalPosition = wire.LocalPosition,
                LocalHorizontalVelocity = wire.LocalHorizontalVelocity,
                VerticalVelocity = wire.VerticalVelocity
            });
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Unreliable)]
        private void ReceiveAuthoritativeStateClientRpc(HumanoidMovementSnapshot snapshot)
        {
            if (IsServer || snapshot.Sequence < _newestServerSequence) return; // unreliable: drop reordered snapshots

            Transform? platform = null;
            if (snapshot.HasPlatform)
            {
                if (!snapshot.Platform.TryGet(out NetworkObject platformObject)) return;
                platform = platformObject.transform;
            }

            _newestServerSequence = snapshot.Sequence;
            _pendingServerState = new HumanoidAuthoritativeState(snapshot.Sequence, snapshot.TeleportEpoch, platform,
                snapshot.LocalPosition, snapshot.LocalHorizontalVelocity, snapshot.VerticalVelocity);
            _hasPendingServerState = true;
            RecordAckLatency(snapshot.Sequence);
        }

        /// <summary>Time from sending an input to hearing the server applied it: round trip plus the server's input buffer.</summary>
        private void RecordAckLatency(uint sequence)
        {
            if (_inputSendTimes.TryGetValue(sequence, out float sentAt))
            {
                PredictionStats.AddAckLatency((Time.realtimeSinceStartup - sentAt) * 1000f);
            }

            _ackedSequences.Clear();
            foreach (var sent in _inputSendTimes.Keys)
            {
                if (sent <= sequence) _ackedSequences.Add(sent);
            }
            foreach (var acked in _ackedSequences) _inputSendTimes.Remove(acked);
        }

        private readonly List<uint> _ackedSequences = new();
        private HumanoidMovementUseCase? _movementUseCase;

        [Inject]
        public void InjectPlayerSpawner(INetworkPlayerSpawner spawner)
        {
            _spawner = spawner;
        }

        [Inject]
        public void InjectMovement(HumanoidMovementUseCase movementUseCase)
        {
            _movementUseCase = movementUseCase;
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

            // The owner predicts its own motion; transform sync keeps driving everyone else.
            GetComponent<NetworkTransformMediator>().OwnerPredicted = true;

            _spawner.NotifyPlayerSpawned(gameObject, OwnerClientId, IsOwner);
        }

        // DefaultExecutionOrder(-100): runs after NGO has moved the interpolated ship (PreLateUpdate) and before the
        // camera follows the player (ThirdPersonLookView.LateUpdate).
        private void LateUpdate()
        {
            if (!IsSpawned) return;

            if (IsLocallyPredicted) _movementUseCase?.CarryWithPlatform(this);

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
                    LastProcessedInputSequence = LastProcessedSequence
                }
                : new PlayerAttachmentState
                {
                    IsAttached = false,
                    LastProcessedInputSequence = LastProcessedSequence
                };
        }

        public bool HasTag(GameplayTag tag) => _abilitySync.HasTag(tag);

        public void AddTag(GameplayTag tag) => _abilitySync.AddTag(tag);

        public void RemoveTag(GameplayTag tag) => _abilitySync.RemoveTag(tag);
        public void AddEffectTag(GameplayTag tag) => _abilitySync.AddEffectTag(tag);
        public void RemoveEffectTag(GameplayTag tag) => _abilitySync.RemoveEffectTag(tag);
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
