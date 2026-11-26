#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Features.Encounters;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Server: the storm's gusts shove the ship sideways (plan: storm-crossing.md, ST3). A flight force
    /// (<see cref="IAirshipFlightForces"/>): inside a raging storm the ship gets the gust blowing where it is
    /// (<see cref="StormWindProcessor.Gust"/>), from the zone's seed and the shared clock, so the sound every peer plays
    /// (<see cref="StormSoundPresenter"/>) swells with the same gust. Only the ship moves; nobody is thrown off the deck.
    /// </summary>
    public sealed class StormGustUseCase : IAirshipFlightForces
    {
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly StormConfig _config;
        private readonly EncounterZoneConfig _zoneConfig;
        private readonly Func<IEncounterZone, IStormHeart?> _heartOf;

        public StormGustUseCase(INetworkService network, IActorRegistry actors, ITimeService time, StormConfig config,
            EncounterZoneConfig zoneConfig, Func<IEncounterZone, IStormHeart?> heartOf)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _config = config;
            _zoneConfig = zoneConfig;
            _heartOf = heartOf;
        }

        [Inject]
        public StormGustUseCase(INetworkService network, IActorRegistry actors, ITimeService time, StormConfig config,
            EncounterZoneConfig zoneConfig)
            : this(network, actors, time, config, zoneConfig, StormHearts.Of)
        {
        }

        /// <summary>The push given at the last tick (the dashboard's and the tests' view of it).</summary>
        public Vector3 LastGust { get; private set; }

        public AirshipFlightForce Evaluate(IAirshipView ship, float engineSpeed, float momentum, float deltaTime)
        {
            LastGust = Vector3.zero;
            if (!_network.IsServer || ship.Transform == null) return default;

            Vector3 position = ship.Transform.position;
            if (StormHearts.WindAt(_actors.GetActors<IEncounterZone>(), position, _heartOf) is not { } wind) return default;

            LastGust = StormWindProcessor.Gust(wind.Zone.Centre, position, wind.Fury, _zoneConfig.Severity, _config.Seed, _time.Time, _config);
            return AirshipFlightForce.Push(LastGust);
        }
    }
}
