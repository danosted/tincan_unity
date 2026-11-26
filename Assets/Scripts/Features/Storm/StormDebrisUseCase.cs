#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Features.Encounters;
using TinCan.Features.SkyHazards;
using UnityEngine;
using VContainer;
using Random = System.Random;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Server: the storm throws debris at the ship (plan: storm-crossing.md, ST4). Inside a raging storm, at a rate by its
    /// fury and the zone's severity (<see cref="StormWindProcessor.DebrisRate"/>), a piece is launched from upwind of each
    /// ship, carried on the storm's swirling wind, aimed a little off where the ship will be, so some hit and some miss.
    /// The pieces are sky hazards (<see cref="ISkyHazards.Launch"/>): one costs the ship's health on contact, and a beam or
    /// cannon hit breaks it. Each leaves the sky once it has blown past.
    /// </summary>
    public sealed class StormDebrisUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly ISkyHazards? _hazards;
        private readonly StormConfig _config;
        private readonly EncounterZoneConfig _zoneConfig;
        private readonly Func<IEncounterZone, IStormHeart?> _heartOf;
        private readonly Random _random;
        private readonly Dictionary<Guid, float> _due = new();

        public StormDebrisUseCase(INetworkService network, IActorRegistry actors, ITimeService time, ISkyHazards? hazards,
            StormConfig config, EncounterZoneConfig zoneConfig, Func<IEncounterZone, IStormHeart?> heartOf, Random random)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _hazards = hazards;
            _config = config;
            _zoneConfig = zoneConfig;
            _heartOf = heartOf;
            _random = random;
        }

        [Inject]
        public StormDebrisUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IObjectResolver resolver,
            StormConfig config, EncounterZoneConfig zoneConfig)
            : this(network, actors, time, resolver.TryResolve<ISkyHazards>(out var hazards) ? hazards : null, config, zoneConfig,
                StormHearts.Of, new Random())
        {
        }

        /// <summary>Pieces launched since the host started.</summary>
        public int Launched { get; private set; }

        public void Tick()
        {
            if (!_network.IsServer || _hazards == null) return;

            foreach (var ship in _actors.GetActors<IAirshipView>())
            {
                if (!ship.IsSimulating || ship.Transform == null) continue;
                Vector3 position = ship.Transform.position;
                if (StormHearts.WindAt(_actors.GetActors<IEncounterZone>(), position, _heartOf) is not { } wind)
                {
                    _due.Remove(ship.Id);
                    continue;
                }

                _due.TryGetValue(ship.Id, out float due);
                due += StormWindProcessor.DebrisRate(wind.Fury, _zoneConfig.Severity, _config) * _time.DeltaTime;
                for (; due >= 1f; due -= 1f) Throw(wind.Zone, ship, position);
                _due[ship.Id] = due;
            }
        }

        /// <summary>
        /// Server: one piece dead at the ship, from <paramref name="distance"/> m upwind (scenarios: a hit that is sure).
        /// False outside a raging storm, or with no sky hazards.
        /// </summary>
        public bool ThrowAt(IAirshipView ship, float distance)
        {
            if (!_network.IsServer || _hazards == null || ship.Transform == null) return false;
            Vector3 position = ship.Transform.position;
            if (StormHearts.WindAt(_actors.GetActors<IEncounterZone>(), position, _heartOf) is not { } wind) return false;
            return Throw(wind.Zone, ship, position, Vector2.zero, distance);
        }

        private bool Throw(IEncounterZone zone, IAirshipView ship, Vector3 position) =>
            Throw(zone, ship, position, new Vector2(Spread() * _config.DebrisMiss.x, Spread() * _config.DebrisMiss.y), null);

        private bool Throw(IEncounterZone zone, IAirshipView ship, Vector3 position, Vector2 miss, float? distance)
        {
            var (start, velocity, lifetime) = StormWindProcessor.Debris(zone.Centre, position, ship.Velocity, miss, _config, distance);
            if (_hazards!.Launch(start, velocity, lifetime) == null) return false;
            Launched++;
            return true;
        }

        private float Spread() => (float)(_random.NextDouble() * 2.0 - 1.0);
    }
}
