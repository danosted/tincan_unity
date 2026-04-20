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
        public event Action<IPossessable, ulong> OnPossessionReceived;
        public event Action<IPossessable, ulong> OnPossessionLost;


        public void RequestPossession(PossessionRequest.Request request)
        {
            // We need to convert the interface target to a NetworkObject for transmission
            if (request.Target is not MonoBehaviour targetMono || !targetMono.TryGetComponent(out NetworkObject targetNetObj))
            {
                Debug.LogError("[PossessionApi] Target is not a networked component.");
                return;
            }

            NetworkObjectReference[] currentPossessionRef = new NetworkObjectReference[0];
            if (request.CurrentPossession is MonoBehaviour currentMono && currentMono.TryGetComponent(out NetworkObject currentNetObj))
            {
                currentPossessionRef = new NetworkObjectReference[] { currentNetObj };
            }

            RequestPossessionServerRpc(targetNetObj, currentPossessionRef);
        }

        [Rpc(SendTo.Server)]
        private void RequestPossessionServerRpc(NetworkObjectReference targetRef, NetworkObjectReference[] currentPossessionsRefArray, RpcParams rpcParams = default)
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
            possessable.AuthoritativeSetPossessor(senderId);

            if (currentPossessionsRefArray != null && currentPossessionsRefArray.Length > 0)
            {
                var currentPossessionRef = currentPossessionsRefArray[0];
                if (currentPossessionRef.TryGet(out NetworkObject currentPossession))
                {
                    if (currentPossession.TryGetComponent(out IPossessable oldPossessable))
                    {
                        currentPossession.ChangeOwnership(0); // Revert ownership to server or neutral
                        oldPossessable.AuthoritativeSetPossessor(null);

                        // Notify clients of the loss of possession
                        NotifyPossessionLossClientRpc(currentPossession, senderId);
                    }
                }
            }

            // Notify all clients of the change
            NotifyPossessionReceivedClientRpc(target, senderId);
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyPossessionReceivedClientRpc(NetworkObjectReference targetRef, ulong newOwnerClientId)
        {
            if (!targetRef.TryGet(out NetworkObject target)) return;

            if (target.TryGetComponent(out IPossessable possessable))
            {
                OnPossessionReceived?.Invoke(possessable, newOwnerClientId);
            }
        }
        [Rpc(SendTo.Everyone)]
        private void NotifyPossessionLossClientRpc(NetworkObjectReference targetRef, ulong newOwnerClientId)
        {
            if (!targetRef.TryGet(out NetworkObject target)) return;

            if (target.TryGetComponent(out IPossessable possessable))
            {
                OnPossessionLost?.Invoke(possessable, newOwnerClientId);
            }
        }
    }
}
