using System;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Possession.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: NGO Implementation of IPossessionApi.
    /// Handles authoritative possession requests via RPCs.
    /// </summary>
    public class PossessionApi : NetworkBehaviour, IPossessionApi
    {
        public event Action<IPossessable, ulong> OnPossessionChanged;

        public void RequestPossession(PossessionRequest.Request request)
        {
            // We need to convert the interface target to a NetworkObject for transmission
            if (request.Target is not MonoBehaviour targetMono || !targetMono.TryGetComponent(out NetworkObject targetNetObj))
            {
                Debug.LogError("[PossessionApi] Target is not a networked component.");
                return;
            }

            RequestPossessionServerRpc(targetNetObj);
        }

        [Rpc(SendTo.Server)]
        private void RequestPossessionServerRpc(NetworkObjectReference targetRef, RpcParams rpcParams = default)
        {
            if (!targetRef.TryGet(out NetworkObject target)) return;
            if (!target.TryGetComponent(out IPossessable possessable)) return;

            ulong senderId = rpcParams.Receive.SenderClientId;

            // Authoritative validation
            if (!possessable.CanPossess(senderId))
            {
                Debug.LogWarning($"[PossessionApi] Possession request for {target.name} from Player {senderId} denied.");
                return;
            }

            // Authoritatively change ownership in NGO
            target.ChangeOwnership(senderId);

            // Notify all clients of the change
            NotifyPossessionChangeClientRpc(target, senderId);
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyPossessionChangeClientRpc(NetworkObjectReference targetRef, ulong newOwnerClientId)
        {
            if (!targetRef.TryGet(out NetworkObject target)) return;

            if (target.TryGetComponent(out IPossessable possessable))
            {
                OnPossessionChanged?.Invoke(possessable, newOwnerClientId);
            }
        }
    }
}
