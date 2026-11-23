#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.Encounters
{
    /// <summary>
    /// Server, after the ships move: puts the configured zone into the world once, then each tick moves every zone on in
    /// its round by whether a ship is inside it (<see cref="EncounterZoneProcessor"/>), on the shared clock, and says so
    /// (<see cref="EncounterZoneChangedEvent"/>). Nothing runs in a zone yet: the storm is its event (encounters.md E2).
    /// </summary>
    public sealed class EncounterZoneUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private readonly EncounterZoneConfig _config;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEncounterZoneSpawner _spawner;
        private readonly IEventPublisher _events;
        private bool _spawned;

        public EncounterZoneUseCase(EncounterZoneConfig config, INetworkService network, IActorRegistry actors, ITimeService time,
            IEncounterZoneSpawner spawner, IEventPublisher events)
        {
            _config = config;
            _network = network;
            _actors = actors;
            _time = time;
            _spawner = spawner;
            _events = events;
        }

        public void Tick()
        {
            if (!_network.IsServer)
            {
                _spawned = false;
                return;
            }

            if (!_spawned)
            {
                _spawned = true;
                if (_config.Prefab != null) _spawner.Spawn(_config.Prefab, new Vector3(_config.Centre.x, 0f, _config.Centre.y));
            }

            double now = _time.Time;
            foreach (var zone in _actors.GetActors<IEncounterZone>().ToList())
            {
                if (!zone.IsSimulating) continue;

                var (state, endsAt) = EncounterZoneProcessor.Step(zone.State, zone.StateEndsAt, ShipInside(zone), now, _config);
                if (state == zone.State && endsAt.Equals(zone.StateEndsAt)) continue;

                var from = zone.State;
                zone.ServerSetState(state, endsAt);
                if (state != from) _events.Publish(new EncounterZoneChangedEvent(zone.DisplayName, from, state));
            }
        }

        private bool ShipInside(IEncounterZone zone) => _actors.GetActors<IAirshipView>()
            .Any(ship => ship.IsSimulating && ship.Transform != null && EncounterZoneProcessor.Contains(zone.Centre, zone.Radius, ship.Transform.position));
    }
}
