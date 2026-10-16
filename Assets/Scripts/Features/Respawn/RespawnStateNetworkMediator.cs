#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Entities;
using Unity.Netcode;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// Infrastructure Layer: the RespawnState ship fixture (with an EntityNetworkMediator, which registers it as an actor).
    /// It carries one discrete UI exchange: a player's choice (respawn, or hang on the leash), sent to the server, which
    /// checks it (<see cref="OverboardUseCase"/>). Who is overboard or leashed replicates as gameplay tags, not here.
    /// </summary>
    public class RespawnStateNetworkMediator : NetworkBehaviour, IRespawnRequests
    {
        private readonly List<RespawnRequest> _requests = new();
        private ActorIdentity? _identity;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public void Request(OverboardChoiceKind choice)
        {
            if (!IsSpawned) return;
            RequestRpc((byte)choice);
        }

        public IReadOnlyList<RespawnRequest> TakeRequests()
        {
            if (_requests.Count == 0) return Array.Empty<RespawnRequest>();
            var taken = _requests.ToArray();
            _requests.Clear();
            return taken;
        }

        [Rpc(SendTo.Server)]
        private void RequestRpc(byte choice, RpcParams rpcParams = default)
        {
            if (!Enum.IsDefined(typeof(OverboardChoiceKind), (int)choice)) return;
            _requests.Add(new RespawnRequest(rpcParams.Receive.SenderClientId, (OverboardChoiceKind)choice));
        }
    }
}
