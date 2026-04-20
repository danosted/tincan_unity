#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// Application Layer: Manages the possession of different IPossessable actors.
    /// Handles switching between characters, vehicles, or cameras using the ActorRegistry.
    /// </summary>
    public class PossessionUseCase : IInitializable, System.IDisposable
    {
        private readonly INetworkService _networkService;
        private readonly INetworkPlayerSpawner _spawner;
        private readonly IActorRegistry _registry;
        private readonly System.Func<IPossessionApi> _apiFactory;
        private IPossessable? _playerActor;
        private IPossessable? _currentPossession;

        public IPossessable? CurrentPossession => _currentPossession;
        public IPossessable? PlayerActor => _playerActor;

        public PossessionUseCase(
            INetworkService networkService,
            INetworkPlayerSpawner spawner,
            IActorRegistry registry,
            System.Func<IPossessionApi> apiFactory)
        {
            _networkService = networkService;
            _spawner = spawner;
            _registry = registry;
            _apiFactory = apiFactory;
        }

        public void Initialize()
        {
            Debug.Log("[PossessionUseCase] Initializing...");
            _registry.OnActorUnregistered += HandleActorUnregistered;
            _spawner.OnPlayerSpawned += HandlePlayerSpawned;

            var api = _apiFactory();
            if (api == null) return;

            api.OnPossessionReceived += HandlePossessionReceived;
            api.OnPossessionLost += HandlePossessionLost;
        }

        public void Dispose()
        {
            _registry.OnActorUnregistered -= HandleActorUnregistered;
            _spawner.OnPlayerSpawned -= HandlePlayerSpawned;

            var api = _apiFactory();
            if (api == null) return;

            api.OnPossessionReceived -= HandlePossessionReceived;
            api.OnPossessionLost -= HandlePossessionLost;
        }

        private void HandlePlayerSpawned(GameObject instance, ulong clientId, bool isLocal)
        {
            if (isLocal)
            {
                InitializePlayerActor(instance);
            }
        }

        private void HandleActorUnregistered(IActor actor)
        {
            if (actor is not IPossessable possessable) return;

            if (_playerActor == possessable)
            {
                _playerActor = null;
                Debug.Log($"[PossessionUseCase] Player actor was unregistered.");
            }

            if (_currentPossession == possessable)
            {
                Debug.Log($"[PossessionUseCase] Current possession {possessable} was unregistered/destroyed. Returning to body.");

                // We don't call OnUnpossessed because the object is already gone/going
                _currentPossession = null;

                if (_playerActor != null)
                {
                    PerformLocalPossession(_playerActor);
                }
            }
        }

        public void InitializePlayerActor(GameObject actorGameObject)
        {
            if (_playerActor != null)
            {
                Debug.LogWarning($"[PossessionUseCase] Player actor is already set to {_playerActor}. Updating assignment to {actorGameObject}.");
            }
            var actor = actorGameObject.GetComponent<IPossessable>();
            if (actor == null)
            {
                Debug.LogError($"[PossessionUseCase] Attempted to initialize player actor with {actorGameObject}, but it does not implement IPossessable!");
                return;
            }
            _playerActor = actor;
            Debug.Log($"[PossessionUseCase] Player actor set to: {(_playerActor as MonoBehaviour)?.name ?? "Unknown"}");

            // If we aren't possessing anything, or if we were waiting for our body to spawn
            if (_currentPossession == null)
            {
                PerformLocalPossession(actor);
            }
        }

        private void HandlePossessionLost(IPossessable lostPossession, ulong ownerId)
        {
            if (ownerId != _networkService.LocalClientId) return; // Not our possession that was lost, ignore

            Debug.Log($"[PossessionUseCase] Possession of {_currentPossession} was lost (ownership changed to another player or revoked). Returning to body.");
            _currentPossession = null;

            if (_playerActor != null)
            {
                PerformLocalPossession(_playerActor);
            }
        }

        private void HandlePossessionReceived(IPossessable target, ulong newOwnerId)
        {
            if (newOwnerId == _networkService.LocalClientId)
            {
                // Filter: If we already predicted this, don't do it again
                if (_currentPossession == target) return;

                Debug.Log($"[PossessionUseCase] Server confirmed possession of {target}");
                PerformLocalPossession(target);
                return;
            }

            if (_currentPossession != target) return;

            // We lost possession of our current actor to someone else (or it was revoked)
            Debug.Log($"[PossessionUseCase] Possession of {_currentPossession} lost to Player {newOwnerId}. Falling back to player actor.");
            _currentPossession = null;

            // Fallback to body if we have one
            if (_playerActor != null && _playerActor != target)
            {
                PerformLocalPossession(_playerActor);
            }
        }

        public void SwitchToNext()
        {
            ulong localId = _networkService.LocalClientId;

            // Get all possessables we are allowed to have
            var possessables = _registry.GetActors<IPossessable>()
                .Where(p => p.CanPossess(localId))
                .ToList();

            // Ensure the primary player actor is always in the consideration pool
            if (_playerActor != null && !possessables.Contains(_playerActor))
            {
                possessables.Insert(0, _playerActor);
            }

            if (possessables.Count <= 1 && _currentPossession == _playerActor)
            {
                Debug.Log("[PossessionUseCase] No other allowed possessable actors to switch to.");
                return;
            }

            int currentIndex = _currentPossession != null ? possessables.IndexOf(_currentPossession) : -1;
            int nextIndex = (currentIndex + 1) % possessables.Count;

            Possess(possessables[nextIndex]);
        }

        public void Possess(IPossessable? target)
        {
            if (target == null)
            {
                // If we try to possess "nothing", return to the body
                if (_playerActor != null) Possess(_playerActor);
                return;
            }

            // 1. Authoritative Request via the API
            var api = _apiFactory();
            if (api != null)
            {
                api.RequestPossession(new PossessionRequest.Request
                {
                    Target = target,
                    CurrentPossession = CurrentPossession
                });
            }

            // 2. Local Prediction
            if (target.CanPossess(_networkService.LocalClientId))
            {
                PerformLocalPossession(target);
            }
        }

        private void PerformLocalPossession(IPossessable target)
        {
            if (_currentPossession != null && _currentPossession != target)
            {
                NotifyReceivers(_currentPossession, false, 0);
            }

            _currentPossession = target;
            if (_currentPossession != null)
            {
                NotifyReceivers(_currentPossession, true, _networkService.LocalClientId);
            }
        }

        private void NotifyReceivers(IPossessable target, bool possessed, ulong playerId)
        {
            if (target is not MonoBehaviour mono) return;

            // Automatically find and notify all receivers in the hierarchy
            var receivers = mono.GetComponentsInChildren<IPossessionReceiver>(true);
            Debug.Log($"[PossessionUseCase] Notifying {receivers.Length} receivers of {(possessed ? "possession" : "unpossession")} of {target} by Player {playerId}.");
            Debug.Log($"[PossessionUseCase] Receivers: {string.Join(", ", receivers.Select(r => r.GetType().Name))}");
            foreach (var receiver in receivers)
            {
                if (possessed) receiver.OnPossessed(playerId);
                else receiver.OnUnpossessed();
            }
        }
    }
}
