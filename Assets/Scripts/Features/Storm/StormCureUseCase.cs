#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Features.Encounters;
using TinCan.Features.Weapons.Cannon;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Storm
{
    /// <summary>Server: a beam hit the storm's target; <see cref="HitsLeft"/> more cure it.</summary>
    public readonly struct StormTargetHitEvent
    {
        public readonly string Zone;
        public readonly long Slot;
        public readonly int HitsLeft;

        public StormTargetHitEvent(string zone, long slot, int hitsLeft)
        {
            Zone = zone;
            Slot = slot;
            HitsLeft = hitsLeft;
        }

        public override string ToString() => $"{Zone}: target {Slot} hit, {HitsLeft} to go";
    }

    /// <summary>
    /// Server: the storm's heart and its cure (plan: storm-crossing.md, ST5). While a zone's round is in its warning the
    /// heart is set to rage and to float a little above the ship sailing in. While the round runs, its target is in the
    /// beam's way (<see cref="ISpiritBeamTargets"/>): a beam that reaches the live target before anything solid hits it,
    /// the slot's target is spent, and the storm's strength falls by one share. The last hit cures it: the round ends
    /// (the zone rests or is gone, by its config) and the storm comes apart on every peer as its strength reaches 0.
    /// Severity comes from the zone's config; where the heart and its targets are from <see cref="StormHeartProcessor"/>.
    /// </summary>
    public sealed class StormCureUseCase : ISimulationTickable, ISpiritBeamTargets
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "Storm";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly StormConfig _config;
        private readonly EncounterZoneConfig _zoneConfig;
        private readonly Func<IEncounterZone, IStormHeart?> _heartOf;
        private (IEncounterZone Zone, IStormHeart Heart, long Slot)? _aimed;

        public StormCureUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events, StormConfig config,
            EncounterZoneConfig zoneConfig, Func<IEncounterZone, IStormHeart?> heartOf)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _config = config;
            _zoneConfig = zoneConfig;
            _heartOf = heartOf;
        }

        [Inject]
        public StormCureUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events, StormConfig config,
            EncounterZoneConfig zoneConfig)
            : this(network, actors, time, events, config, zoneConfig, StormHearts.Of)
        {
        }

        private int Severity => _zoneConfig.Severity;

        public void Tick()
        {
            if (!_network.IsServer) return;

            foreach (var zone in _actors.GetActors<IEncounterZone>().ToList())
            {
                if (!zone.IsSimulating || zone.State != EncounterZoneState.Warning || _heartOf(zone) is not { } heart) continue;
                if (ShipInside(zone) is not { } ship) continue;

                float altitude = ship.Transform.position.y + _config.HeartAboveShip;
                if (heart.Strength < 1f || heart.HitSlot != -1 || Mathf.Abs(heart.Altitude - altitude) > 0.5f) heart.ServerReset(altitude);
            }
        }

        public float? FirstHit(Vector3 origin, Vector3 direction, float length)
        {
            _aimed = null;
            if (!_network.IsServer) return null;

            float? nearest = null;
            foreach (var zone in _actors.GetActors<IEncounterZone>())
            {
                if (!zone.IsSimulating || zone.State != EncounterZoneState.Running || _heartOf(zone) is not { } heart || heart.Strength <= 0f) continue;
                if (StormHeartProcessor.LiveTarget(zone.Centre, heart.Altitude, Severity, _config.Seed, _time.Time, heart.HitSlot, _config)
                    is not { } target) continue;

                if (StormHeartProcessor.Enters(origin, direction, length, target.Position, _config.TargetRadius) is not { } distance) continue;
                if (nearest is { } best && distance >= best) continue;

                nearest = distance;
                _aimed = (zone, heart, target.Slot);
            }
            return nearest;
        }

        public void Answer(Vector3 origin, Vector3 direction, float distance)
        {
            if (_aimed is not { } aimed) return;
            _aimed = null;

            int hits = StormHeartProcessor.HitsToCure(Severity, _config);
            int left = Mathf.Max(0, Mathf.RoundToInt(aimed.Heart.Strength * hits) - 1);
            aimed.Heart.ServerHit(aimed.Slot, left / (float)hits);
            _events.Publish(new StormTargetHitEvent(aimed.Zone.DisplayName, aimed.Slot, left));
            _events.LogInfo(LogSource, $"{aimed.Zone.DisplayName}: target {aimed.Slot} hit, {left} to go.");
            if (left > 0) return;

            var (state, endsAt) = EncounterZoneProcessor.End(_time.Time, _zoneConfig);
            var from = aimed.Zone.State;
            aimed.Zone.ServerSetState(state, endsAt);
            _events.Publish(new EncounterZoneChangedEvent(aimed.Zone.DisplayName, from, state));
            _events.LogInfo(LogSource, $"{aimed.Zone.DisplayName} cured.");
        }

        private IAirshipView? ShipInside(IEncounterZone zone)
        {
            foreach (var ship in _actors.GetActors<IAirshipView>())
            {
                if (ship.IsSimulating && ship.Transform != null && EncounterZoneProcessor.Contains(zone.Centre, zone.Radius, ship.Transform.position))
                    return ship;
            }
            return null;
        }
    }
}
