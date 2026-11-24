#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Core.Humanoid;
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// Server, on the simulation tick. Two bounds:
    /// - **The ship's:** a ship <see cref="CloudBoundaryConfig.EmergencyDepth"/> below the surface starts an emergency;
    ///   climbing back out clears it, and when it runs out the ship is lost with its whole crew
    ///   (<see cref="IShipLossSource"/>; the voyage ends on it). The last pose where the ship was safe, above the open
    ///   sea, is kept to respawn it there. Bringing the ship back (<see cref="IShipRecovery"/>) clears the emergency.
    /// - **A player's:** someone <see cref="CloudBoundaryConfig.CharacterResetDepth"/> below the surface who is not with
    ///   a ship (not on its deck or inside it) fell overboard: an <see cref="IOverboardHandler"/> takes them, or they are
    ///   teleported home. The crew aboard a sinking ship share its fate instead.
    /// Plan: .docs/plans/ship-lost-and-respawn.md.
    /// </summary>
    public class CloudBoundaryUseCase : ISimulationTickable, IShipLossSource, IShipRecovery
    {
        private readonly struct SafePose
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public SafePose(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }
        }

        private readonly INetworkService _networkService;
        private readonly IActorRegistry _actorRegistry;
        private readonly ITimeService _timeService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ICloudSurfaceQuery _surfaceQuery;
        private readonly IHumanoidRespawnService _respawnService;
        private readonly CloudBoundaryProcessor _processor;
        private readonly CloudBoundaryConfig _config;
        private readonly IReadOnlyList<IOverboardHandler> _overboardHandlers;
        private readonly Dictionary<Guid, CloudEmergencyState> _airshipStates = new();
        private readonly Dictionary<Guid, SafePose> _safePoses = new();

        public CloudBoundaryUseCase(
            INetworkService networkService,
            IActorRegistry actorRegistry,
            ITimeService timeService,
            IEventPublisher eventPublisher,
            ICloudSurfaceQuery surfaceQuery,
            IHumanoidRespawnService respawnService,
            CloudBoundaryProcessor processor,
            CloudBoundaryConfig config,
            IReadOnlyList<IOverboardHandler> overboardHandlers)
        {
            _networkService = networkService;
            _actorRegistry = actorRegistry;
            _timeService = timeService;
            _eventPublisher = eventPublisher;
            _surfaceQuery = surfaceQuery;
            _respawnService = respawnService;
            _processor = processor;
            _config = config;
            _overboardHandlers = overboardHandlers;
        }

        public SimulationPhase Phase => SimulationPhase.BeforeHumanoid;

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

        public bool IsLost(IAirshipView ship) => _airshipStates.TryGetValue(ship.Id, out var state) && state.HasExpired;

        public bool TryGetSafePose(IAirshipView ship, out Vector3 position, out Quaternion rotation)
        {
            bool known = _safePoses.TryGetValue(ship.Id, out var pose);
            position = pose.Position;
            rotation = pose.Rotation;
            return known;
        }

        /// <summary>The ship is back: its countdown starts afresh. The last safe pose is kept.</summary>
        public void Recover(IAirshipView ship) => _airshipStates.Remove(ship.Id);

        private void EvaluateAirships(IEnumerable<IAirshipView> airships)
        {
            float lowestSea = _config.BaseAltitude - _config.HeightAmplitude - 1f;
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

                if (!current.IsActive && _processor.IsSafe(position.y, surfaceHeight, lowestSea))
                {
                    _safePoses[airship.Id] = new SafePose(position, AirshipUpright.Of(airship.Transform.rotation));
                }

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
                }
            }
        }

        /// <summary>
        /// When false, characters below the cloud surface are left where they are instead of being teleported back to
        /// the ship. The network test harness turns it off so a sinking ship does not respawn its bots every tick.
        /// </summary>
        public bool CharacterResetEnabled { get; set; } = true;

        private void ResetFallenCharacters(IReadOnlyCollection<IAirshipView> airships)
        {
            if (!CharacterResetEnabled || airships.Count == 0)
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

                // With a ship (on its deck, or inside it) the player shares the ship's bound, not their own.
                if (IsWithAShip(character, airships)) continue;

                // A feature may take the fallen player instead (the overboard choice); otherwise they are teleported home.
                if (_overboardHandlers.Any(handler => handler.TryHandleOverboard(character))) continue;

                IAirshipView nearestAirship = airships
                    .OrderBy(airship => (airship.Transform.position - position).sqrMagnitude)
                    .First();
                var (respawnPosition, respawnRotation) = AirshipBoardingPose.Resolve(nearestAirship, _config.FallbackRespawnOffset);
                _respawnService.ResetCharacter(character, respawnPosition, respawnRotation);
                _eventPublisher.Publish(new CloudCharacterResetEvent(character.Id, nearestAirship.Id));
            }
        }

        // The movement layer keeps the ship as the character's moving ground while they stand on it or are inside its
        // volume, a jump included.
        private static bool IsWithAShip(IHumanoidCharacterView character, IEnumerable<IAirshipView> airships)
        {
            Transform? ground = character.Movement.CurrentGround.MovingGroundTransform;
            return ground != null && airships.Any(airship => airship.Transform != null && ground.IsChildOf(airship.Transform));
        }
    }
}
