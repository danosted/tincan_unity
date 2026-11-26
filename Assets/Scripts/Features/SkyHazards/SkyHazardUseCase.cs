#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>
    /// Application Layer, server only, after airship movement. Hazards appear only on demand: <see cref="SpawnAt"/>
    /// (scenarios and events) hangs one where it is put; <see cref="Launch"/> sends one flying at a fixed velocity until
    /// its lifetime runs out (a storm's debris on its wind; the hazard's NetworkTransformMediator carries the motion to
    /// clients). Removes hazards that were shot down (after a short delay, so their last health update reaches the
    /// clients first). Any hazard the ship touches applies the impact effect to the ship (its health) and is removed. The
    /// field around the ship and homing were removed in H0 (.docs/plans/hazards-and-boons.md); comets (H1) build on the
    /// moving hazard.
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
        private readonly IShipContactQuery _contact;
        private readonly AbilitySystemUseCase _abilities;
        private readonly SkyHazardConfig _config;
        private readonly List<ISkyHazard> _alive = new();
        private readonly Dictionary<ISkyHazard, float> _destroyedAt = new();
        private readonly Dictionary<ISkyHazard, (Vector3 Velocity, float GoneAt)> _flying = new();
        private float _elapsed;

        public SkyHazardUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            ISkyHazardSpawner spawner, IShipContactQuery contact, AbilitySystemUseCase abilities, SkyHazardConfig config)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _spawner = spawner;
            _contact = contact;
            _abilities = abilities;
            _config = config;
        }

        public IReadOnlyList<ISkyHazard> Alive => _alive;
        public int Destroyed { get; private set; }
        public int Hits { get; private set; }

        /// <summary>Session start: every hazard leaves the sky.</summary>
        public void ResetForSession()
        {
            if (!_network.IsServer) return;
            foreach (var hazard in _alive) _spawner.Despawn(hazard);
            _alive.Clear();
            _destroyedAt.Clear();
            _flying.Clear();
        }

        /// <summary>Nothing runs on its own, so the session's pressure starting or stopping changes nothing here.</summary>
        public void SetSessionActive(bool active) { }

        public ISkyHazard? SpawnAt(Vector3 position)
        {
            if (!_network.IsServer) return null;
            var hazard = _spawner.Spawn(position);
            if (hazard == null) return null;

            _alive.Add(hazard);
            return hazard;
        }

        public ISkyHazard? Launch(Vector3 position, Vector3 velocity, float lifetime)
        {
            var hazard = SpawnAt(position);
            if (hazard != null) _flying[hazard] = (velocity, _elapsed + Mathf.Max(0f, lifetime));
            return hazard;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;
            _elapsed += _time.DeltaTime;

            _alive.RemoveAll(Vanished);
            RemoveDestroyed();
            Fly();

            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.IsSimulating && candidate.Transform != null);
            if (ship != null) Strike(ship);
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
                _alive.RemoveAt(i);
                Remove(hazard);
            }
        }

        // Moving hazards fly on, and leave the sky when their time is up. A shot-down one stops where it broke.
        private void Fly()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var hazard = _alive[i];
                if (!_flying.TryGetValue(hazard, out var flight) || IsShotDown(hazard)) continue;

                if (_elapsed >= flight.GoneAt)
                {
                    _alive.RemoveAt(i);
                    Remove(hazard);
                    continue;
                }
                hazard.Transform!.position += flight.Velocity * _time.DeltaTime;
            }
        }

        // Gone from the world by other means (its object destroyed): forget it.
        private bool Vanished(ISkyHazard hazard)
        {
            if (hazard.Transform != null) return false;
            _flying.Remove(hazard);
            _destroyedAt.Remove(hazard);
            return true;
        }

        private void Remove(ISkyHazard hazard)
        {
            _flying.Remove(hazard);
            _destroyedAt.Remove(hazard);
            _spawner.Despawn(hazard);
        }

        // Every live hazard the ship touches hurts it once and is removed.
        private void Strike(IAirshipView ship)
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var hazard = _alive[i];
                var transform = hazard.Transform;
                if (IsShotDown(hazard) || transform == null) continue;
                if (!_contact.Touches(ship, transform.position, _config.ContactRadius)) continue;

                Hits++;
                var controller = (ship as IShipState)?.Controller;
                if (controller != null && _config.ImpactEffect != null) _abilities.ApplyEffect(controller, _config.ImpactEffect);
                _events.Publish(new SkyHazardHitShipEvent(ship.Id, Hits));
                _events.LogInfo(LogSource, $"A hazard hit the ship ({Hits} so far).");

                _alive.RemoveAt(i);
                Remove(hazard);
            }
        }

        private static bool IsShotDown(ISkyHazard hazard) => hazard.Controller.IsDepleted();
    }
}
