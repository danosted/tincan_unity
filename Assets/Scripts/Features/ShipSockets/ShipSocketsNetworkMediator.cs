#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Sockets;
using Unity.Collections;
using Unity.Netcode;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Infrastructure Layer: a ship's mounted fittings on the ShipSocketsState fixture (with an EntityNetworkMediator,
    /// which registers it as an actor). A server-written list replicates what is mounted where, late joiners included.
    /// Two RPCs carry the discrete UI exchange: the server asks one player to choose a fitting, and the player's choice
    /// comes back as a request the server validates (<see cref="ShipFittingUseCase"/>).
    /// </summary>
    public class ShipSocketsNetworkMediator : NetworkBehaviour, IShipFittingState
    {
        private NetworkList<MountedFitting> _mounted = null!;
        private readonly List<MountedFitting> _snapshot = new();
        private readonly List<MountRequest> _requests = new();
        private ShipSocketId? _chooser;
        private ActorIdentity? _identity;

        private void Awake() => _mounted = new NetworkList<MountedFitting>();

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public IAirshipView? Ship => transform.parent != null ? transform.parent.GetComponentInParent<IAirshipView>() : null;

        public IReadOnlyList<MountedFitting> Mounted
        {
            get
            {
                _snapshot.Clear();
                foreach (var fitting in _mounted) _snapshot.Add(fitting);
                return _snapshot;
            }
        }

        public void ServerSetMounted(IReadOnlyList<MountedFitting> mounted)
        {
            if (!IsServer) return;
            _mounted.Clear();
            foreach (var fitting in mounted) _mounted.Add(fitting);
        }

        public void ServerOpenChooser(ulong clientId, ShipSocketId socket)
        {
            if (!IsServer) return;
            OpenChooserRpc(socket.PartInstanceId, socket.Index, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        public bool TryTakeChooser(out ShipSocketId socket)
        {
            socket = _chooser ?? default;
            if (_chooser == null) return false;
            _chooser = null;
            return true;
        }

        public void RequestMount(ShipSocketId socket, string fittingId)
        {
            if (!IsSpawned) return;
            RequestMountRpc(socket.PartInstanceId, socket.Index, new FixedString64Bytes(fittingId));
        }

        public IReadOnlyList<MountRequest> TakeRequests()
        {
            if (_requests.Count == 0) return Array.Empty<MountRequest>();
            var taken = _requests.ToArray();
            _requests.Clear();
            return taken;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void OpenChooserRpc(int partInstanceId, int socketIndex, RpcParams rpcParams) =>
            _chooser = new ShipSocketId(partInstanceId, socketIndex);

        [Rpc(SendTo.Server)]
        private void RequestMountRpc(int partInstanceId, int socketIndex, FixedString64Bytes fittingId, RpcParams rpcParams = default) =>
            _requests.Add(new MountRequest(rpcParams.Receive.SenderClientId, new ShipSocketId(partInstanceId, socketIndex), fittingId.ToString()));

        public override void OnDestroy()
        {
            _mounted?.Dispose();
            base.OnDestroy();
        }
    }
}
