#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// Application Layer, server: lays and tracks the gas field (<see cref="GasFieldProcessor"/> for the spots,
    /// <see cref="IGasPocketSpawner"/> for the pockets, which <see cref="GasChallengeUseCase"/> then detonates like any
    /// other). Spots in rock (<see cref="IWorldObstacleQuery"/>) are moved or dropped. A new session, or the end of one,
    /// clears the field.
    /// </summary>
    public sealed class GasFieldUseCase : IGasField, ISessionParticipant
    {
        private const string LogSource = "GasChallenge";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IGasPocketSpawner _spawner;
        private readonly GasFieldProcessor _processor;
        private readonly GasChallengeConfig _config;
        private readonly IEventPublisher _events;
        private readonly IReadOnlyList<IWorldObstacleQuery> _obstacles;
        private readonly System.Random _random;
        private readonly List<IGasPocket> _pockets = new();

        public GasFieldUseCase(INetworkService network, IActorRegistry actors, IGasPocketSpawner spawner, GasFieldProcessor processor,
            GasChallengeConfig config, IEventPublisher events, IRandomSource random, IEnumerable<IWorldObstacleQuery> obstacles)
        {
            _network = network;
            _actors = actors;
            _spawner = spawner;
            _processor = processor;
            _config = config;
            _events = events;
            _random = random.Create("GasField");
            _obstacles = obstacles.ToList();
        }

        public IReadOnlyList<IGasPocket> Pockets => _pockets;

        public bool IsCleared
        {
            get
            {
                var ship = Ship();
                if (ship == null) return true;

                var heading = _processor.Heading(ship.Velocity, ship.Transform.forward);
                return _pockets.All(pocket => IsGone(pocket) || pocket.Detonated
                    || _processor.IsPassed(pocket.Center, ship.Transform.position, heading, _config.PassedDistance));
            }
        }

        public int Spawn(int count, float ahead, float spread)
        {
            if (!_network.IsServer) return 0;
            Clear();

            var ship = Ship();
            if (ship == null) return 0;

            var heading = _processor.Heading(ship.Velocity, ship.Transform.forward);
            float clearance = PocketRadius();
            var spots = _processor.Spots(ship.Transform.position, heading, count, ahead, spread, _config.FieldDepth, _config.FieldHeight,
                _random, spot => _obstacles.Any(query => query.IsBlocked(spot, clearance)), _config.PlacementTries);

            foreach (var spot in spots)
            {
                var pocket = _spawner.Spawn(spot);
                if (pocket != null) _pockets.Add(pocket);
            }

            _events.LogInfo(LogSource, $"Gas field: {_pockets.Count} of {count} pocket(s) {ahead:0} m ahead.");
            return _pockets.Count;
        }

        public void Clear()
        {
            foreach (var pocket in _pockets)
            {
                if (!IsGone(pocket)) _spawner.Despawn(pocket);
            }
            _pockets.Clear();
        }

        public void ResetForSession() => Clear();

        public void SetSessionActive(bool active)
        {
            if (!active) Clear();
        }

        private static bool IsGone(IGasPocket pocket) => pocket is Component component && component == null;

        private IAirshipView? Ship() => _actors.GetActors<IAirshipView>().FirstOrDefault(ship => ship.IsSimulating && ship.Transform != null);

        private float PocketRadius() => _config.PocketPrefab != null && _config.PocketPrefab.TryGetComponent<GasPocketVolume>(out var volume) ? volume.Radius : 3f;
    }
}
