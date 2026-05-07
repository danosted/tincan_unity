using System;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Possession.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: NGO Implementation of IPossessionNetworkMediator.
    /// Handles possession requests via RPCs.
    /// </summary>
    public class PossessionNetworkMediator : NetworkBehaviour, IPossessionNetworkMediator
    {
        public event Action<IPossessable, ulong> OnPossessionReceived;
        public event Action<IPossessable, ulong> OnPossessionLost;
        public event Action<IPossessable> OnPossessionDenied;

        public event Action<ulong, NetworkObjectReference, NetworkObjectReference[]> OnServerPossessionRequested;

        public void RequestPossession(PossessionRequest.Request request)
        {
            // We need to convert the interface target to a NetworkObject for transmission
            if (request.Target is not MonoBehaviour targetMono || !targetMono.TryGetComponent(out NetworkObject targetNetObj))
            {
                Debug.LogError("[PossessionNetworkMediator] Target is not a networked component.");
                return;
            }

            NetworkObjectReference[] currentPossessionRef = new NetworkObjectReference[0];
            if (request.CurrentPossession is MonoBehaviour currentMono && currentMono.TryGetComponent(out NetworkObject currentNetObj))
            {
                currentPossessionRef = new NetworkObjectReference[] { currentNetObj };
            }

            Debug.Log("[PossessionNetworkMediator] request received.");
            RequestPossessionServerRpc(targetNetObj, currentPossessionRef);
        }

        [Rpc(SendTo.Server)]
        private void RequestPossessionServerRpc(NetworkObjectReference targetRef, NetworkObjectReference[] currentPossessionsRefArray, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[PossessionNetworkMediator] Server request received from client {senderId}.");
            OnServerPossessionRequested?.Invoke(senderId, targetRef, currentPossessionsRefArray);
        }

        public void NotifyPossessionReceived(NetworkObjectReference targetRef, ulong newOwnerClientId)
        {
            NotifyPossessionReceivedClientRpc(targetRef, newOwnerClientId);
        }

        public void NotifyPossessionLost(NetworkObjectReference targetRef, ulong newOwnerClientId)
        {
            NotifyPossessionLossClientRpc(targetRef, newOwnerClientId);
        }

        public void NotifyPossessionDenied(NetworkObjectReference targetRef, ulong senderId)
        {
            RpcParams rpcParams = RpcTarget.Single(senderId, RpcTargetUse.Temp);
            NotifyPossessionDeniedClientRpc(targetRef, rpcParams);
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

        [Rpc(SendTo.SpecifiedInParams)]
        private void NotifyPossessionDeniedClientRpc(NetworkObjectReference targetRef, RpcParams rpcParams = default)
        {
            if (!targetRef.TryGet(out NetworkObject target)) return;

            if (target.TryGetComponent(out IPossessable possessable))
            {
                OnPossessionDenied?.Invoke(possessable);
            }
        }
    }
}
