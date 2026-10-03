#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Core.Ship;
using TinCan.Features.Stations;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Helm
{
    /// <summary>
    /// Infrastructure Layer: the helm fixture on a ship, a station (interact to take it, IA_TakeHelm). The helmsman
    /// stays in their body at the Seat child and keeps their own view (no station camera). Server-written state: the
    /// occupant's client id, so every peer and a late joiner sees who steers. The steering itself travels in the
    /// helmsman's own predicted input (<see cref="HumanoidInputState.StationAxes"/>); see <see cref="HelmSteeringUseCase"/>.
    /// </summary>
    public class HelmNetworkMediator : NetworkBehaviour, IHelm, IStation
    {
        private const ulong NoOccupant = ulong.MaxValue;

        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [Tooltip("Runs on the helmsman while they steer (GA_OccupyHelm).")]
        [SerializeField] private AbilityDefinition? _occupyAbility;

        private readonly NetworkVariable<ulong> _occupantClientId = new(NoOccupant);
        private ActorIdentity? _identity;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public InteractionDefinition Definition => _interactionDefinition!;
        public AbilityDefinition? OccupyAbility => _occupyAbility;
        public IReadOnlyList<AbilityDefinition> GrantedAbilities => Array.Empty<AbilityDefinition>();
        public Camera? ViewCamera => null;
        public ulong? OccupantClientId => _occupantClientId.Value == NoOccupant ? null : _occupantClientId.Value;

        public Transform? Seat { get; private set; }

        // Fixtures are parented to the ship root after they spawn, so look the ship up when asked.
        public IAirshipView? Ship => this != null ? GetComponentInParent<IAirshipView>() : null;

        private void Awake()
        {
            Seat = transform.FindDescendant("Seat");
            if (Seat == null) Debug.LogError($"[{nameof(HelmNetworkMediator)}] {name} needs a Seat child.", this);
        }

        public void ServerSetOccupant(IHumanoidCharacterView? occupant)
        {
            if (!IsServer) return;
            _occupantClientId.Value = occupant is Component component && component != null && component.TryGetComponent(out NetworkObject body)
                ? body.OwnerClientId
                : NoOccupant;
        }
    }
}
