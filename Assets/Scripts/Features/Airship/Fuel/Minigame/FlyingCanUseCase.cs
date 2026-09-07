#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>
    /// Server-only world pickup field around the first simulating airship. Seeds a scattered volume on arrival,
    /// extends it at the horizon as the ship travels, and removes only distant cans. Never moves a can.
    /// </summary>
    public class FlyingCanUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private readonly INetworkService _networkService;
        private readonly IActorRegistry _actorRegistry;
        private readonly IFlyingCanSpawner _spawner;
        private readonly FlyingCanWaveProcessor _waves;
        private readonly FlyingCanConfig _config;
        private readonly System.Random _random;
        private readonly List<IFlyingCanView> _cans = new();
        private readonly List<IAirshipView> _ships = new();
        private Guid? _shipId;
        private Vector3 _lastPosition;
        private Vector3 _seedPosition;
        private Quaternion _seedRotation;
        private int _nextSeedRow;

        [Inject]
        public FlyingCanUseCase(INetworkService networkService, IActorRegistry actorRegistry,
            IFlyingCanSpawner spawner, FlyingCanWaveProcessor waves, FlyingCanConfig config)
            : this(networkService, actorRegistry, spawner, waves, config, new System.Random()) { }

        public FlyingCanUseCase(INetworkService networkService, IActorRegistry actorRegistry,
            IFlyingCanSpawner spawner, FlyingCanWaveProcessor waves, FlyingCanConfig config, System.Random random)
        {
            _networkService = networkService;
            _actorRegistry = actorRegistry;
            _spawner = spawner;
            _waves = waves;
            _config = config;
            _random = random;
        }

        public void Tick()
        {
            if (!_networkService.IsServer || _config == null || !_config.Enabled) return;

            _ships.Clear();
            _ships.AddRange(_actorRegistry.GetActors<IAirshipView>().Where(a => a.IsSimulating && a.Transform != null));
            if (_ships.Count == 0)
            {
                _shipId = null;
                return;
            }

            var airship = _ships[0];
            Vector3 position = airship.Transform.position;
            if (_shipId != airship.Id)
            {
                _shipId = airship.Id;
                _seedPosition = _lastPosition = position;
                _seedRotation = airship.Transform.rotation;
                _nextSeedRow = 0;
            }

            CullDistantCans();
            float spacing = Mathf.Max(1f, _config.RowSpacing);
            float horizon = Mathf.Max(spacing, _config.AheadDistance);
            float initialAhead = Mathf.Clamp(_config.InitialAheadDistance, 0f, horizon);
            int lastSeedRow = Mathf.CeilToInt((horizon - initialAhead) / spacing);
            while (_nextSeedRow <= lastSeedRow)
            {
                if (!TrySpawnPair(_seedPosition, _seedRotation, Mathf.Min(initialAhead + _nextSeedRow * spacing, horizon))) return;
                _nextSeedRow++;
            }

            if (!_waves.ShouldSpawn(_lastPosition, position, spacing)) return;
            Quaternion rotation = _waves.TravelRotation(position - _lastPosition, airship.Transform.rotation);
            if (TrySpawnPair(position, rotation, horizon)) _lastPosition = position;
        }

        private void CullDistantCans()
        {
            _cans.Clear();
            _cans.AddRange(_actorRegistry.GetActors<IFlyingCanView>());
            // Keep the entire spawn volume inside the retention radius, even with unusual Inspector values.
            Vector3 extent = new(Mathf.Max(Mathf.Abs(_config.LateralMin), Mathf.Abs(_config.LateralMax)),
                Mathf.Max(Mathf.Abs(_config.HeightMin), Mathf.Abs(_config.HeightMax)),
                Mathf.Max(_config.AheadDistance, _config.RowSpacing) + Mathf.Abs(_config.DepthSpread));
            float distance = Mathf.Max(_config.DespawnDistance, extent.magnitude + 1f);
            for (int i = _cans.Count - 1; i >= 0; i--)
            {
                var can = _cans[i];
                if (can.Transform == null) { _cans.RemoveAt(i); continue; }
                Vector3 position = can.Transform.position;
                if (_ships.Any(ship => (ship.Transform.position - position).sqrMagnitude <= distance * distance)) continue;
                _spawner.Despawn(can);
                _cans.RemoveAt(i);
            }
        }

        private bool TrySpawnPair(Vector3 position, Quaternion rotation, float ahead)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (_cans.Count >= _config.MaxAlive) return true;
                // Bounded retries find an open spot without forcing a can into a ship or an existing pickup.
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector3 candidate = _waves.ComputeSpawn(position, rotation, ahead,
                        (float)_random.NextDouble(), (float)_random.NextDouble(), (float)_random.NextDouble(), side, _config.SpawnParameters);
                    if (_ships.Any(ship => !_waves.IsClearOfShip(candidate, ship.Transform.position, _config.MinimumShipDistance))) continue;
                    float separation = Mathf.Max(0.1f, _config.MinimumSeparation);
                    if (_cans.Any(can => (can.Transform.position - candidate).sqrMagnitude < separation * separation)) continue;
                    var spawned = _spawner.Spawn(candidate);
                    if (spawned == null) return false;
                    _cans.Add(spawned);
                    break;
                }
            }
            return true;
        }
    }
}
