#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Entities;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Encounters
{
    /// <summary>
    /// Infrastructure Layer: an encounter zone in the world (with an EntityNetworkMediator). It replicates its centre, its
    /// state and when that state ends; its object stands at the centre (its placeholder look, a ring of pillars at the
    /// wall, is a child). Its name and radius come from its config. The server writes it (<see cref="EncounterZoneUseCase"/>).
    /// </summary>
    public class EncounterZoneNetworkMediator : NetworkBehaviour, IEncounterZone
    {
        [SerializeField] private EncounterZoneConfig? _config;

        private readonly NetworkVariable<Vector2> _centre = new(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<byte> _state = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _endsAt = new(0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private ActorIdentity? _identity;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;
        public string DisplayName => _config != null ? _config.DisplayName : name;
        public Vector2 Centre => _centre.Value;
        public float Radius => _config != null ? _config.Radius : 0f;
        public EncounterZoneState State => (EncounterZoneState)_state.Value;
        public double StateEndsAt => _endsAt.Value;

        public void ServerSetCentre(Vector2 centre)
        {
            if (IsServer) _centre.Value = centre;
        }

        public void ServerSetState(EncounterZoneState state, double endsAt)
        {
            if (!IsServer) return;
            _state.Value = (byte)state;
            _endsAt.Value = endsAt;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _centre.Value = new Vector2(transform.position.x, transform.position.z);
            _centre.OnValueChanged += OnCentreChanged;
            Stand(_centre.Value);
        }

        public override void OnNetworkDespawn() => _centre.OnValueChanged -= OnCentreChanged;

        private void OnCentreChanged(Vector2 previous, Vector2 centre) => Stand(centre);

        private void Stand(Vector2 centre) => transform.position = new Vector3(centre.x, transform.position.y, centre.y);
    }
}
