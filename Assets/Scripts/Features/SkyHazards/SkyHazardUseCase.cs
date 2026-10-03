#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using UnityEngine;
using VContainer;

namespace TinCan.Features.SkyHazards
{
    /// <summary>
    /// Application Layer, server only, after airship movement. Removes hazards that were shot down (after a short
    /// delay, so their last health update reaches the clients first). Drifting hazards home on the first ship; any
    /// hazard that touches it applies the impact effect to the ship (its health) and is removed. While the field is on,
    /// it keeps a field of drifting hazards in a box ahead of the ship, sized for the crew aboard (each extra player
    /// raises the limit and speeds up spawning), removing those left far behind.
    /// </summary>
    public class SkyHazardUseCase : ISimulationTickable, ISkyHazards, ISessionParticipant
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "SkyHazards";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly ISkyHazardSpawner _spawner;
        private readonly SkyHazardFieldProcessor _field;
        private readonly HazardDriftProcessor _drift;
        private readonly IShipContactQuery _contact;
        private readonly AbilitySystemUseCase _abilities;
        private readonly SkyHazardConfig _config;
        private readonly System.Random _random;
        private readonly List<ISkyHazard> _alive = new();
        private readonly HashSet<ISkyHazard> _drifting = new();
        private readonly Dictionary<ISkyHazard, float> _destroyedAt = new();
        private float _elapsed;
        private float _nextSpawnAt;

        [Inject]
        public SkyHazardUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            ISkyHazardSpawner spawner, SkyHazardFieldProcessor field, HazardDriftProcessor drift, IShipContactQuery contact,
            AbilitySystemUseCase abilities, SkyHazardConfig config, IRandomSource random)
            : this(network, actors, time, events, spawner, field, drift, contact, abilities, config, random.Create("SkyHazards")) { }

        public SkyHazardUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            ISkyHazardSpawner spawner, SkyHazardFieldProcessor field, HazardDriftProcessor drift, IShipContactQuery contact,
            AbilitySystemUseCase abilities, SkyHazardConfig config, System.Random random)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _spawner = spawner;
            _field = field;
            _drift = drift;
            _contact = contact;
            _abilities = abilities;
            _config = config;
            _random = random;
            FieldEnabled = config.FieldEnabled;
        }

        public IReadOnlyList<ISkyHazard> Alive => _alive;
        public int Destroyed { get; private set; }
        public int Hits { get; private set; }
        public bool FieldEnabled { get; set; }

        /// <summary>Session start: every hazard leaves the sky.</summary>
        public void ResetForSession()
        {
            if (!_network.IsServer) return;
            foreach (var hazard in _alive) _spawner.Despawn(hazard);
            _alive.Clear();
            _drifting.Clear();
            _destroyedAt.Clear();
            _nextSpawnAt = _elapsed;
        }

        /// <summary>The field runs only while the session is underway, and only where the config switches it on.</summary>
        public void SetSessionActive(bool active) => FieldEnabled = active && _config.FieldEnabled;

        public ISkyHazard? SpawnAt(Vector3 position, bool drifts = false)
        {
            if (!_network.IsServer) return null;
            var hazard = _spawner.Spawn(position);
            if (hazard == null) return null;

            _alive.Add(hazard);
            if (drifts) _drifting.Add(hazard);
            return hazard;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;
            _elapsed += _time.DeltaTime;

            _alive.RemoveAll(hazard => hazard.Transform == null);
            _drifting.RemoveWhere(hazard => hazard.Transform == null);
            RemoveDestroyed();

            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.IsSimulating && candidate.Transform != null);
            if (ship == null) return;

            DriftAndStrike(ship);
            if (!FieldEnabled) return;

            RemoveDistant(ship.Transform.position);
            var (maxAlive, spawnInterval) = _field.ForCrew(_actors.CrewCount(), _config.CrewScaling);
            if (_alive.Count < maxAlive && _elapsed >= _nextSpawnAt)
            {
                var position = _field.SpawnPoint(ship.Transform.position, ship.Transform.rotation, _config.FieldShape,
                    (float)_random.NextDouble(), (float)_random.NextDouble(), (float)_random.NextDouble());
                SpawnAt(position, drifts: true);
                _nextSpawnAt = _elapsed + spawnInterval;
            }
        }

        private void RemoveDestroyed()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var hazard = _alive[i];
                if (!IsShotDown(hazard)) continue;

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
                _drifting.Remove(hazard);
                _spawner.Despawn(hazard);
            }
        }

        private void DriftAndStrike(IAirshipView ship)
        {
            Vector3? target = null;
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var hazard = _alive[i];
                var transform = hazard.Transform;
                if (IsShotDown(hazard) || transform == null) continue;

                if (_drifting.Contains(hazard))
                {
                    target ??= _contact.Center(ship);
                    transform.position = _drift.Step(transform.position, target.Value, _config.DriftSpeed, _time.DeltaTime);
                }

                if (!_contact.Touches(ship, transform.position, _config.ContactRadius)) continue;

                Strike(ship);
                _alive.RemoveAt(i);
                _drifting.Remove(hazard);
                _spawner.Despawn(hazard);
            }
        }

        private void Strike(IAirshipView ship)
        {
            Hits++;
            var controller = (ship as IShipState)?.Controller;
            if (controller != null && _config.ImpactEffect != null) _abilities.ApplyEffect(controller, _config.ImpactEffect);

            _events.Publish(new SkyHazardHitShipEvent(ship.Id, Hits));
            _events.LogInfo(LogSource, $"A hazard hit the ship ({Hits} so far).");
        }

        private void RemoveDistant(Vector3 shipPosition)
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var hazard = _alive[i];
                if (IsShotDown(hazard) || !_field.IsTooFar(shipPosition, hazard.Transform!.position, _config.RemoveDistance)) continue;
                _alive.RemoveAt(i);
                _drifting.Remove(hazard);
                _spawner.Despawn(hazard);
            }
        }

        private static bool IsShotDown(ISkyHazard hazard) => hazard.Controller.IsDepleted();
    }
}
