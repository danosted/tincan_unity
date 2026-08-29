#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Airship;
using TinCan.Features.HumanoidMovement;
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    public class CloudBoundaryUseCase
    {
        private readonly INetworkService _networkService;
        private readonly IActorRegistry _actorRegistry;
        private readonly ITimeService _timeService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ICloudSurfaceQuery _surfaceQuery;
        private readonly ICloudBoundaryExpiryHandler _expiryHandler;
        private readonly IHumanoidRespawnService _respawnService;
        private readonly CloudBoundaryProcessor _processor;
        private readonly CloudBoundaryConfig _config;
        private readonly Dictionary<Guid, CloudEmergencyState> _airshipStates = new();

        public CloudBoundaryUseCase(
            INetworkService networkService,
            IActorRegistry actorRegistry,
            ITimeService timeService,
            IEventPublisher eventPublisher,
            ICloudSurfaceQuery surfaceQuery,
            ICloudBoundaryExpiryHandler expiryHandler,
            IHumanoidRespawnService respawnService,
            CloudBoundaryProcessor processor,
            CloudBoundaryConfig config)
        {
            _networkService = networkService;
            _actorRegistry = actorRegistry;
            _timeService = timeService;
            _eventPublisher = eventPublisher;
            _surfaceQuery = surfaceQuery;
            _expiryHandler = expiryHandler;
            _respawnService = respawnService;
            _processor = processor;
            _config = config;
        }

        public void Tick()
        {
            if (!_networkService.IsServer)
            {
                return;
            }

            List<IAirshipView> airships = _actorRegistry.GetActors<IAirshipView>()
                .Where(airship => airship.IsSimulating)
                .ToList();

            EvaluateAirships(airships);
            ResetFallenCharacters(airships);
        }

        private void EvaluateAirships(IEnumerable<IAirshipView> airships)
        {
            foreach (IAirshipView airship in airships)
            {
                _airshipStates.TryGetValue(airship.Id, out CloudEmergencyState previous);
                Vector3 position = airship.Transform.position;
                float surfaceHeight = _surfaceQuery.GetSurfaceHeight(position.x, position.z);
                CloudEmergencyState current = _processor.EvaluateAirship(
                    previous,
                    position.y,
                    surfaceHeight,
                    _config.EmergencyDepth,
                    _config.RecoveryMargin,
                    _config.EmergencyDuration,
                    _timeService.DeltaTime);

                _airshipStates[airship.Id] = current;

                if (!previous.IsActive && current.IsActive)
                {
                    _eventPublisher.Publish(new CloudEmergencyStartedEvent(airship.Id, current.RemainingTime));
                }

                if (previous.IsActive && !current.IsActive)
                {
                    _eventPublisher.Publish(new CloudEmergencyClearedEvent(airship.Id));
                }

                if (!previous.HasExpired && current.HasExpired)
                {
                    _eventPublisher.Publish(new CloudEmergencyExpiredEvent(airship.Id));
                    _expiryHandler.HandleExpiry(airship.Id);
                }
            }
        }

        private void ResetFallenCharacters(IReadOnlyCollection<IAirshipView> airships)
        {
            if (airships.Count == 0)
            {
                return;
            }

            foreach (IHumanoidCharacterView character in _actorRegistry.GetActors<IHumanoidCharacterView>())
            {
                Vector3 position = character.Movement.Transform.position;
                float surfaceHeight = _surfaceQuery.GetSurfaceHeight(position.x, position.z);
                if (position.y > surfaceHeight - _config.CharacterResetDepth)
                {
                    continue;
                }

                IAirshipView nearestAirship = airships
                    .OrderBy(airship => (airship.Transform.position - position).sqrMagnitude)
                    .First();
                ResolveRespawnPose(nearestAirship, out Vector3 respawnPosition, out Quaternion respawnRotation);
                _respawnService.ResetCharacter(character, respawnPosition, respawnRotation);
                _eventPublisher.Publish(new CloudCharacterResetEvent(character.Id, nearestAirship.Id));
            }
        }

        private void ResolveRespawnPose(IAirshipView airship, out Vector3 position, out Quaternion rotation)
        {
            IAirshipRespawnPoint? respawnPoint = airship.Transform.GetComponentInChildren<IAirshipRespawnPoint>(true);
            if (respawnPoint != null)
            {
                position = respawnPoint.Position;
                rotation = respawnPoint.Rotation;
                return;
            }

            position = airship.Transform.TransformPoint(_config.FallbackRespawnOffset);
            rotation = airship.Transform.rotation;
        }
    }
}
