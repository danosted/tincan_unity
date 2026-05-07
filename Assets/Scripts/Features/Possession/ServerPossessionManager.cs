#nullable enable
using System;
using TinCan.Core.Domain.Networking;
using Unity.Netcode;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// Server Layer: Authoritative manager for possession requests.
    /// Listens to the network mediator and performs validation and NGO ownership changes.
    /// </summary>
    /// <summary>
    /// Server Layer: Authoritative manager for possession requests.
    /// Listens to the network mediator and performs validation and NGO ownership changes.
    /// </summary>
    public class ServerPossessionManager : IDisposable
    {
        private readonly Func<IPossessionNetworkMediator> _mediatorFactory;
        private IPossessionNetworkMediator? _currentMediator;

        public ServerPossessionManager(Func<IPossessionNetworkMediator> mediatorFactory)
        {
            _mediatorFactory = mediatorFactory;
        }

        public void Subscribe()
        {
            if (_currentMediator != null)
            {
                _currentMediator.OnServerPossessionRequested -= HandlePossessionRequested;
            }

            Debug.Log("[ServerPossessionManager] Authoritative service started. Subscribing to mediator.");
            _currentMediator = _mediatorFactory();
            _currentMediator.OnServerPossessionRequested += HandlePossessionRequested;
        }

        public void Dispose()
        {
            if (_currentMediator != null)
            {
                _currentMediator.OnServerPossessionRequested -= HandlePossessionRequested;
            }
        }

        private void HandlePossessionRequested(ulong senderId, NetworkObjectReference targetRef, NetworkObjectReference[] currentPossessionsRefArray)
        {
            if (!targetRef.TryGet(out NetworkObject target)) return;
            if (!target.TryGetComponent(out IPossessable possessable)) return;

            // Authoritative validation
            if (!possessable.CanPossess(senderId))
            {
                Debug.LogWarning($"[ServerPossessionManager] Possession request for {target.name} from Player {senderId} denied.");
                _currentMediator?.NotifyPossessionDenied(targetRef, senderId);
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
                        _currentMediator?.NotifyPossessionLost(currentPossession, senderId);
                    }
                }
            }

            // Notify all clients of the change
            _currentMediator?.NotifyPossessionReceived(target, senderId);
        }
    }
}
