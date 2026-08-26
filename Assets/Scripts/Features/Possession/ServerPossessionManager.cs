#nullable enable
using System;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Domain;
using TinCan.Features.Airship;
using Unity.Netcode;
using UnityEngine;
using VContainer.Unity;
using System.Collections.Generic;

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
    public class ServerPossessionManager : IDisposable, IPossessionAuthority
    {
        private readonly Func<IPossessionNetworkMediator> _mediatorFactory;
        private readonly NetworkManager _networkManager;
        private readonly IActorRegistry _actorRegistry;
        private IPossessionNetworkMediator? _currentMediator;
        private readonly Dictionary<ulong, NetworkObject> _activePossessions = new();

        public ServerPossessionManager(
            Func<IPossessionNetworkMediator> mediatorFactory,
            NetworkManager networkManager,
            IActorRegistry actorRegistry)
        {
            _mediatorFactory = mediatorFactory;
            _networkManager = networkManager;
            _actorRegistry = actorRegistry;
        }

        public void Subscribe()
        {
            if (_currentMediator != null)
            {
                _currentMediator.OnServerPossessionRequested -= HandlePossessionRequested;
                _currentMediator.OnServerPossessionReleaseRequested -= HandlePossessionReleaseRequested;
            }

            Debug.Log("[ServerPossessionManager] Authoritative service started. Subscribing to mediator.");
            _currentMediator = _mediatorFactory();
            _currentMediator.OnServerPossessionRequested += HandlePossessionRequested;
            _currentMediator.OnServerPossessionReleaseRequested += HandlePossessionReleaseRequested;
        }

        public void Dispose()
        {
            if (_currentMediator != null)
            {
                _currentMediator.OnServerPossessionRequested -= HandlePossessionRequested;
                _currentMediator.OnServerPossessionReleaseRequested -= HandlePossessionReleaseRequested;
            }
        }

        private void HandlePossessionRequested(ulong senderId, NetworkObjectReference targetRef, NetworkObjectReference[] currentPossessionsRefArray)
        {
            if (!targetRef.TryGet(out NetworkObject target)) return;
            if (!target.TryGetComponent(out IPossessable possessable)) return;

            if (!TryResolveRequesterActor(senderId, out var requesterActor)) return;

            TryAcquirePossession(requesterActor.Id, possessable);
        }

        private void HandlePossessionReleaseRequested(ulong senderId)
        {
            if (TryResolveRequesterActor(senderId, out var requesterActor))
            {
                TryReleasePossession(requesterActor.Id);
            }
        }

        public bool TryAcquirePossession(Guid requesterActorId, IPossessable target)
        {
            if (!_networkManager.IsServer || target is not MonoBehaviour targetMono ||
                !targetMono.TryGetComponent(out NetworkObject targetObject))
            {
                return false;
            }

            if (!_actorRegistry.TryGetActor(requesterActorId, out var requesterActor) ||
                requesterActor is not MonoBehaviour requesterMono ||
                !requesterMono.TryGetComponent(out NetworkObject requesterObject))
            {
                return false;
            }

            ulong clientId = requesterObject.OwnerClientId;

            if (target.PossessorId.HasValue && target.PossessorId.Value != clientId)
            {
                Debug.LogWarning($"[ServerPossessionManager] Possession request for {targetObject.name} from Player {clientId} denied.");
                _currentMediator?.NotifyPossessionDenied(targetObject, clientId);
                return false;
            }

            ReleaseCurrentPossession(clientId, targetObject);

            if (targetObject.OwnerClientId != clientId)
            {
                targetObject.ChangeOwnership(clientId);
            }
            target.AuthoritativeSetPossessor(clientId);
            _activePossessions[clientId] = targetObject;

            _currentMediator?.NotifyPossessionReceived(targetObject, clientId);
            Debug.Log($"[ServerPossessionManager] Granted possession of {targetObject.name} to client {clientId}.");
            return true;
        }

        public bool TryReleasePossession(Guid requesterActorId)
        {
            if (!_networkManager.IsServer ||
                !_actorRegistry.TryGetActor(requesterActorId, out var requesterActor) ||
                requesterActor is not MonoBehaviour requesterMono ||
                !requesterMono.TryGetComponent(out NetworkObject requesterObject))
            {
                return false;
            }

            ulong clientId = requesterObject.OwnerClientId;
            if (!_activePossessions.TryGetValue(clientId, out var possession))
            {
                return false;
            }

            ReleasePossession(clientId, possession, true);
            RestorePlayerPossession(clientId);
            Debug.Log($"[ServerPossessionManager] Released possession of {possession.name} from client {clientId}.");
            return true;
        }

        private void RestorePlayerPossession(ulong clientId)
        {
            if (!_networkManager.ConnectedClients.TryGetValue(clientId, out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent(out IPossessable playerBody))
            {
                return;
            }

            playerBody.AuthoritativeSetPossessor(clientId);
            _currentMediator?.NotifyPossessionReceived(client.PlayerObject, clientId);
        }

        private bool TryResolveRequesterActor(ulong clientId, out IActor actor)
        {
            actor = null!;
            if (!_networkManager.ConnectedClients.TryGetValue(clientId, out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent(out actor))
            {
                return false;
            }

            return true;
        }

        private void ReleaseCurrentPossession(ulong clientId, NetworkObject nextPossession)
        {
            if (_activePossessions.TryGetValue(clientId, out var previousPossession) && previousPossession != nextPossession)
            {
                ReleasePossession(clientId, previousPossession, true);
            }

            if (_networkManager.ConnectedClients.TryGetValue(clientId, out var client) &&
                client.PlayerObject != null && client.PlayerObject != nextPossession)
            {
                ReleasePossession(clientId, client.PlayerObject, false);
            }
        }

        private void ReleasePossession(ulong clientId, NetworkObject possession, bool releaseOwnership)
        {
            if (!possession.TryGetComponent(out IPossessable possessable)) return;

            if (releaseOwnership && possession.OwnerClientId == clientId)
            {
                possession.ChangeOwnership(NetworkManager.ServerClientId);
            }

            if (possessable is IAirshipView airship)
            {
                airship.InputState = new AirshipInputState();
            }

            possessable.AuthoritativeSetPossessor(null);
            _activePossessions.Remove(clientId);
            _currentMediator?.NotifyPossessionLost(possession, clientId);
        }
    }
}
