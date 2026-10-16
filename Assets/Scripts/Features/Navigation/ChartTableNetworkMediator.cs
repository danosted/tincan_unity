#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Core.Ship;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Infrastructure Layer: the chart table on a ship, a station (interact to take it, IA_TakeChartTable). The navigator
    /// stays in their body at the Seat child and looks down at the table through its ViewCamera (switched on for them
    /// only). Server-written state: the occupant's client id, so every peer and a late joiner sees who navigates; the route
    /// itself lives on the ship's NavigationState. Children found by name: Seat, Chart (the chart's centre on the table
    /// top, scale 1), ViewCamera.
    /// </summary>
    public class ChartTableNetworkMediator : NetworkBehaviour, IChartTable
    {
        private const ulong NoOccupant = ulong.MaxValue;

        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [Tooltip("Runs on the navigator while they sit at the table (GA_OccupyChartTable).")]
        [SerializeField] private AbilityDefinition? _occupyAbility;

        private readonly NetworkVariable<ulong> _occupantClientId = new(NoOccupant);
        private ActorIdentity? _identity;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public InteractionDefinition Definition => _interactionDefinition!;
        public AbilityDefinition? OccupyAbility => _occupyAbility;
        public IReadOnlyList<AbilityDefinition> GrantedAbilities => Array.Empty<AbilityDefinition>();
        public Camera? ViewCamera { get; private set; }
        public ulong? OccupantClientId => _occupantClientId.Value == NoOccupant ? null : _occupantClientId.Value;
        public Transform? Seat { get; private set; }
        public Transform? Chart { get; private set; }

        // Fixtures are parented to the ship root after they spawn, so look the ship up when asked.
        public IAirshipView? Ship => this != null ? GetComponentInParent<IAirshipView>() : null;

        private void Awake()
        {
            Seat = transform.FindDescendant("Seat");
            Chart = transform.FindDescendant("Chart");
            ViewCamera = transform.FindDescendant("ViewCamera")?.GetComponent<Camera>();
            if (ViewCamera != null) ViewCamera.enabled = false; // switched on only for the local navigator (StationViewPresenter)
            if (Seat == null || Chart == null) Debug.LogError($"[{nameof(ChartTableNetworkMediator)}] {name} needs Seat and Chart children.", this);
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
