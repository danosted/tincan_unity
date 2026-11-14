#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Entities;
using Unity.Netcode;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// Infrastructure Layer: the holes in the cloud sea on the CloudHolesState ship fixture (with an EntityNetworkMediator,
    /// which registers it as an actor). A server-written list, so every peer and every late joiner has the same holes;
    /// each peer works out where they are now itself (<see cref="CloudHolesUseCase"/>).
    /// </summary>
    public class CloudHolesNetworkMediator : NetworkBehaviour, ICloudHoleState
    {
        private readonly List<CloudHole> _holes = new();
        private NetworkList<CloudHole> _replicated = null!;
        private ActorIdentity? _identity;

        private void Awake() => _replicated = new NetworkList<CloudHole>();

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public IReadOnlyList<CloudHole> Holes => _holes;

        public override void OnNetworkSpawn()
        {
            _replicated.OnListChanged += OnListChanged;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            _replicated.OnListChanged -= OnListChanged;
            _holes.Clear();
        }

        public void ServerAdd(CloudHole hole)
        {
            if (!IsServer) return;
            _replicated.Add(hole);
        }

        public void ServerRemoveAt(int index)
        {
            if (!IsServer || index < 0 || index >= _replicated.Count) return;
            _replicated.RemoveAt(index);
        }

        private void OnListChanged(NetworkListEvent<CloudHole> change) => Refresh();

        private void Refresh()
        {
            _holes.Clear();
            foreach (CloudHole hole in _replicated) _holes.Add(hole);
        }
    }
}
