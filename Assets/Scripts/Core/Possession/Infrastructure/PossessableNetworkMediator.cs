#nullable enable
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Core.Possession.Infrastructure
{
    /// <summary>
    /// Makes an entity possessable: replicates who possesses it (server-written) and gives a player object to its
    /// owner on spawn. The entity's actor (HumanoidPlayer, AirshipNetworkMediator) implements IPossessable by
    /// forwarding here, so only objects with this component are possession candidates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PossessableNetworkMediator : NetworkBehaviour
    {
        private struct OptionalClientId : INetworkSerializable
        {
            public bool HasValue;
            public ulong Value;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref HasValue);
                if (HasValue) serializer.SerializeValue(ref Value);
            }
        }

        private readonly NetworkVariable<OptionalClientId> _possessorId = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public ulong? PossessorId => _possessorId.Value.HasValue ? _possessorId.Value.Value : null;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // A player's own body is possessed by that player from the start.
            if (IsServer && NetworkObject.IsPlayerObject) AuthoritativeSetPossessor(OwnerClientId);
        }

        public bool CanPossess(ulong playerId)
        {
            if (!IsSpawned) return false;
            return !_possessorId.Value.HasValue || _possessorId.Value.Value == playerId;
        }

        public void AuthoritativeSetPossessor(ulong? playerId)
        {
            if (!IsServer) return;
            _possessorId.Value = playerId.HasValue
                ? new OptionalClientId { HasValue = true, Value = playerId.Value }
                : default;
        }
    }
}
