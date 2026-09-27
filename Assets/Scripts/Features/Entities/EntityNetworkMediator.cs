#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Domain.Networking;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Entities
{
    /// <summary>
    /// The entity of one networked object: on the root of every networked prefab, exactly once. It owns the object's
    /// stable id (server-assigned, replicated, or preset by a spawner restoring a saved world) and is the only thing that
    /// registers the object's actors and capabilities: once, after every behaviour on the object has spawned
    /// (<see cref="OnNetworkPostSpawn"/>), and once on despawn.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class EntityNetworkMediator : NetworkBehaviour, IEntity
    {
        private readonly NetworkVariable<NetworkEntityId> _replicatedId = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private IActorOrchestrator? _orchestrator;
        private INetworkService? _network;
        private Guid _serverId;
        private Guid? _presetId;
        private bool _registered;

        [Inject]
        public void Construct(IActorOrchestrator orchestrator, INetworkService network)
        {
            _orchestrator = orchestrator;
            _network = network;
        }

        public GameObject Root => gameObject;

        /// <summary>
        /// On the server the id exists from first use (so behaviours that spawn before this one can read it); clients
        /// read the replicated value, which arrives with the spawn.
        /// </summary>
        public Guid EntityId
        {
            get
            {
                if (!IsAuthority) return _replicatedId.Value.Value;
                if (_serverId == Guid.Empty) _serverId = _presetId ?? EntityIds.New();
                return _serverId;
            }
        }

        private bool IsAuthority => _network?.IsServer ?? IsServer;

        /// <summary>Server only, before spawn: give the entity a known id (a saved world restoring its objects).</summary>
        public void PresetEntityId(Guid id)
        {
            if (IsSpawned) throw new InvalidOperationException($"{name}: the entity id is fixed once spawned.");
            _presetId = id;
            _serverId = Guid.Empty;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer) _replicatedId.Value = new NetworkEntityId(EntityId);
        }

        protected override void OnNetworkPostSpawn()
        {
            base.OnNetworkPostSpawn();
            if (_orchestrator == null)
            {
                Debug.LogError($"[EntityNetworkMediator] {name} was not injected, so nothing on it is registered.", this);
                return;
            }
            _orchestrator.RegisterEntity(this);
            _registered = true;
        }

        public override void OnNetworkDespawn()
        {
            if (_registered) _orchestrator?.UnregisterEntity(this);
            _registered = false;
            base.OnNetworkDespawn();
        }
    }
}
