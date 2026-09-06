#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>
    /// Application Layer: spawns, moves and expires flying jerry cans around the first simulating airship.
    /// Server only; ticked from the network tick after the airship has moved.
    /// </summary>
    public class FlyingCanUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private readonly INetworkService _networkService;
        private readonly IActorRegistry _actorRegistry;
        private readonly ITimeService _timeService;
        private readonly IFlyingCanSpawner _spawner;
        private readonly FlyingCanWaveProcessor _waves;
        private readonly FlyingCanMotionProcessor _motion;
        private readonly FlyingCanConfig _config;
        private readonly FuelConsumptionProcessor _drivenCheck = new();
        private readonly System.Random _random;
        private readonly List<IFlyingCanView> _scratch = new();
        private float _sinceLastSpawn;

        // [Inject] pins the container to this constructor; VContainer would otherwise pick the longer test-only one below.
        [Inject]
        public FlyingCanUseCase(
            INetworkService networkService,
            IActorRegistry actorRegistry,
            ITimeService timeService,
            IFlyingCanSpawner spawner,
            FlyingCanWaveProcessor waves,
            FlyingCanMotionProcessor motion,
            FlyingCanConfig config)
            : this(networkService, actorRegistry, timeService, spawner, waves, motion, config, new System.Random())
        {
        }

        public FlyingCanUseCase(
            INetworkService networkService,
            IActorRegistry actorRegistry,
            ITimeService timeService,
            IFlyingCanSpawner spawner,
            FlyingCanWaveProcessor waves,
            FlyingCanMotionProcessor motion,
            FlyingCanConfig config,
            System.Random random)
        {
            _networkService = networkService;
            _actorRegistry = actorRegistry;
            _timeService = timeService;
            _spawner = spawner;
            _waves = waves;
            _motion = motion;
            _config = config;
            _random = random;
        }

        public void Tick()
        {
            if (!_networkService.IsServer || _config == null) return;

            float now = _timeService.Time;
            float deltaTime = _timeService.DeltaTime;

            int alive = AdvanceCans(now, deltaTime);
            TrySpawn(now, deltaTime, alive);
        }

        private int AdvanceCans(float now, float deltaTime)
        {
            _scratch.Clear();
            _scratch.AddRange(_actorRegistry.GetActors<IFlyingCanView>());

            int alive = 0;
            foreach (var can in _scratch)
            {
                if (_motion.IsExpired(can.SpawnTime, now, _config.Lifetime))
                {
                    _spawner.Despawn(can);
                    continue;
                }

                var transform = can.Transform;
                if (transform == null) continue;

                transform.position = _motion.Step(transform.position, can.Velocity, deltaTime);
                alive++;
            }

            return alive;
        }

        private void TrySpawn(float now, float deltaTime, int alive)
        {
            if (!_config.Enabled) return;

            var airship = _actorRegistry.GetActors<IAirshipView>().FirstOrDefault(a => a.IsSimulating);
            if (airship == null) return;

            if (_config.SpawnOnlyWhileDriven && !_drivenCheck.IsDriven(airship.PossessorId.HasValue, airship.InputState.Throttle)) return;

            _sinceLastSpawn += deltaTime;
            if (!_waves.ShouldSpawn(_sinceLastSpawn, alive, _config.SpawnInterval, _config.MaxAlive)) return;

            var (position, velocity) = _waves.ComputeSpawn(
                airship.Transform.position,
                airship.Transform.rotation,
                (float)_random.NextDouble(),
                (float)_random.NextDouble(),
                _random.Next(0, 2) == 0 ? -1f : 1f,
                _config.SpawnParameters);

            var can = _spawner.Spawn(position, velocity);
            if (can == null) return;

            can.Velocity = velocity;
            can.SpawnTime = now;
            _sinceLastSpawn = 0f;
        }
    }
}
