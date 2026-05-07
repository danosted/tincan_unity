using System;

namespace TinCan.Features.Possession
{
    public interface IPossessionNetworkMediator
    {
        event Action<IPossessable, ulong> OnPossessionReceived;
        event Action<IPossessable, ulong> OnPossessionLost;
        event Action<IPossessable> OnPossessionDenied;

        // Server side event
        event Action<ulong, Unity.Netcode.NetworkObjectReference, Unity.Netcode.NetworkObjectReference[]> OnServerPossessionRequested;

        void RequestPossession(PossessionRequest.Request request);

        // Server side callbacks to trigger RPCs
        void NotifyPossessionReceived(Unity.Netcode.NetworkObjectReference targetRef, ulong newOwnerClientId);
        void NotifyPossessionLost(Unity.Netcode.NetworkObjectReference targetRef, ulong newOwnerClientId);
        void NotifyPossessionDenied(Unity.Netcode.NetworkObjectReference targetRef, ulong senderId);
    }
}
