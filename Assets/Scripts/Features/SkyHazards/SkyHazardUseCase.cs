#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Airship;
using UnityEngine;
using VContainer;

namespace TinCan.Features.SkyHazards
{
    /// <summary>Server-side control over hazards, for scenarios and (later) designed events.</summary>
    public interface ISkyHazards
    {
        /// <summary>Hazards spawned and not yet removed.</summary>
        IReadOnlyList<ISkyHazard> Alive { get; }

        /// <summary>Hazards shot down since the session started.</summary>
        int Destroyed { get; }

        bool FieldEnabled { get; set; }
        ISkyHazard? SpawnAt(Vector3 position);
    }

    /// <summary>Server: a hazard was shot down.</summary>
    public readonly struct SkyHazardDestroyedEvent
    {
        public readonly string Hazard;
        public readonly int Destroyed;

        public SkyHazardDestroyedEvent(string hazard, int destroyed)
        {
            Hazard = hazard;
            Destroyed = destroyed;
        }
    }

    /// <summary>
    /// Application Layer, server only, after airship movement. Removes hazards that were shot down (after a short
    /// delay, so their last health update reaches the clients first) and, while the field is on, keeps up to
    /// MaxAlive of them in a box ahead of the first ship, removing those left far behind. Hazards hang still for now;
    /// drift toward the ship and ship contact come in S2.
    /// </summary>
    public class SkyHazardUseCase : ISimulationTickable, ISkyHazards
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "SkyHazards";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly ISkyHazardSpawner _spawner;
        private readonly SkyHazardFieldProcessor _field;
        private readonly SkyHazardConfig _config;
        private readonly System.Random _random;
        private readonly List<ISkyHazard> _alive = new();
        private readonly Dictionary<ISkyHazard, float> _destroyedAt = new();
        private float _elapsed;
        private float _nextSpawnAt;

        [Inject]
        public SkyHazardUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            ISkyHazardSpawner spawner, SkyHazardFieldProcessor field, SkyHazardConfig config)
            : this(network, actors, time, events, spawner, field, config, new System.Random()) { }

        public SkyHazardUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            ISkyHazardSpawner spawner, SkyHazardFieldProcessor field, SkyHazardConfig config, System.Random random)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _spawner = spawner;
            _field = field;
            _config = config;
            _random = random;
            FieldEnabled = config.FieldEnabled;
        }

        public IReadOnlyList<ISkyHazard> Alive => _alive;
        public int Destroyed { get; private set; }
        public bool FieldEnabled { get; set; }

        public ISkyHazard? SpawnAt(Vector3 position)
        {
            if (!_network.IsServer) return null;
            var hazard = _spawner.Spawn(position);
            if (hazard != null) _alive.Add(hazard);
            return hazard;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;
            _elapsed += _time.DeltaTime;

            _alive.RemoveAll(hazard => hazard.Transform == null);
            RemoveDestroyed();

            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.IsSimulating && candidate.Transform != null);
            if (!FieldEnabled || ship == null) return;

            RemoveDistant(ship.Transform.position);
            if (_alive.Count < _config.MaxAlive && _elapsed >= _nextSpawnAt)
            {
                var position = _field.SpawnPoint(ship.Transform.position, ship.Transform.rotation, _config.FieldShape,
                    (float)_random.NextDouble(), (float)_random.NextDouble(), (float)_random.NextDouble());
                SpawnAt(position);
                _nextSpawnAt = _elapsed + _config.SpawnInterval;
            }
        }

        private void RemoveDestroyed()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var hazard = _alive[i];
                if (!hazard.IsDestroyed) continue;

                if (!_destroyedAt.TryGetValue(hazard, out var at))
                {
                    _destroyedAt[hazard] = _elapsed;
                    Destroyed++;
                    string name = hazard is Component component && component != null ? component.name : "hazard";
                    _events.Publish(new SkyHazardDestroyedEvent(name, Destroyed));
                    _events.LogInfo(LogSource, $"{name} shot down ({Destroyed} so far).");
                    continue;
                }

                if (_elapsed - at < _config.DespawnDelay) continue;
                _destroyedAt.Remove(hazard);
                _alive.RemoveAt(i);
                _spawner.Despawn(hazard);
            }
        }

        private void RemoveDistant(Vector3 shipPosition)
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var hazard = _alive[i];
                if (hazard.IsDestroyed || !_field.IsTooFar(shipPosition, hazard.Transform!.position, _config.RemoveDistance)) continue;
                _alive.RemoveAt(i);
                _spawner.Despawn(hazard);
            }
        }
    }
}
