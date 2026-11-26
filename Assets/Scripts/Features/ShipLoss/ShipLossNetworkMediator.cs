#nullable enable
using System;
using TinCan.Core.Domain.Entities;
using Unity.Netcode;

namespace TinCan.Features.ShipLoss
{
    /// <summary>
    /// Infrastructure Layer: whether the ship is lost, on the ShipLossState ship fixture (with an EntityNetworkMediator,
    /// which registers it as an actor). A server-written variable carries it to every peer, late joiners included; a
    /// server RPC takes Respawn from any player.
    /// </summary>
    public class ShipLossNetworkMediator : NetworkBehaviour, IShipLossState
    {
        private readonly NetworkVariable<bool> _lost = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private ActorIdentity? _identity;
        private bool _respawnRequested;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;
        public bool IsLost => _lost.Value;

        public void RequestRespawn()
        {
            if (!IsSpawned) return;
            if (IsServer) _respawnRequested = true;
            else RequestRespawnRpc();
        }

        public bool ConsumeRespawnRequest()
        {
            bool requested = _respawnRequested;
            _respawnRequested = false;
            return requested;
        }

        public void ServerSetLost(bool lost)
        {
            if (IsServer && _lost.Value != lost) _lost.Value = lost;
        }

        [Rpc(SendTo.Server)]
        private void RequestRespawnRpc() => _respawnRequested = true;
    }
}
