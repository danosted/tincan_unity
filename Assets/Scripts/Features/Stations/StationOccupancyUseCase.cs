#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using UnityEngine;

namespace TinCan.Features.Stations
{
    /// <summary>Server-side occupancy of stations, for the interaction handler, other features and scenarios.</summary>
    public interface IStationOccupancy
    {
        bool TryOccupy(IHumanoidCharacterView player, IStation station);
        bool Leave(Guid playerId);
        IStation? StationOf(Guid playerId);
        IHumanoidCharacterView? OccupantOf(IStation station);
    }

    /// <summary>Server: a player took or left a station.</summary>
    public readonly struct StationOccupancyChangedEvent
    {
        public readonly Guid PlayerId;
        public readonly string Station;
        public readonly bool Occupied;

        public StationOccupancyChangedEvent(Guid playerId, string station, bool occupied)
        {
            PlayerId = playerId;
            Station = station;
            Occupied = occupied;
        }
    }

    /// <summary>
    /// Application Layer, server only. Occupying a station: the player is placed on its seat, its occupy ability runs on
    /// them (the active effect locks walking and carries State.Occupying.*; the ability's cancel and block lists stop
    /// other abilities), its granted abilities are added, and the station records the occupant. Leaving undoes exactly
    /// that. Pressing Interact while occupying leaves (<see cref="IInteractOverride"/>), so leaving is an input bit on the
    /// same tick as everything else. Every tick it also releases a station whose occupant or station has gone.
    /// Grants are server-only: the occupant's client does not predict the station's abilities (S3 polish).
    /// </summary>
    public class StationOccupancyUseCase : IStationOccupancy, IInteractOverride, ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private const string LogSource = "Stations";

        private sealed class Occupancy
        {
            public IHumanoidCharacterView Player = null!;
            public IStation Station = null!;
            public readonly List<AbilityDefinition> Granted = new();
        }

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly AbilitySystemUseCase _abilities;
        private readonly IHumanoidRespawnService _respawn;
        private readonly IEventPublisher _events;
        private readonly Dictionary<Guid, Occupancy> _byPlayer = new();

        public StationOccupancyUseCase(
            INetworkService network,
            IActorRegistry actors,
            AbilitySystemUseCase abilities,
            IHumanoidRespawnService respawn,
            IEventPublisher events)
        {
            _network = network;
            _actors = actors;
            _abilities = abilities;
            _respawn = respawn;
            _events = events;
        }

        public bool TryOccupy(IHumanoidCharacterView player, IStation station)
        {
            if (!_network.IsServer || !IsAlive(station) || station.OccupyAbility == null) return false;
            if (_byPlayer.ContainsKey(player.Id)) return false;
            if (OccupantOf(station) != null)
            {
                _events.LogInfo(LogSource, $"{Name(station)} is already occupied.");
                return false;
            }

            var occupy = station.OccupyAbility;
            if (!_abilities.HasAbility(player, occupy)) _abilities.GrantAbility(player, occupy);
            if (!_abilities.TryActivateAbility(player, occupy))
            {
                _events.LogInfo(LogSource, $"{occupy.name} refused for {Name(station)}.");
                return false;
            }

            var occupancy = new Occupancy { Player = player, Station = station };
            foreach (var ability in station.GrantedAbilities)
            {
                if (ability == null || _abilities.HasAbility(player, ability)) continue;
                _abilities.GrantAbility(player, ability);
                occupancy.Granted.Add(ability);
            }

            _byPlayer[player.Id] = occupancy;
            if (station.Seat != null) _respawn.ResetCharacter(player, station.Seat.position, station.Seat.rotation);
            station.ServerSetOccupant(player);

            _events.Publish(new StationOccupancyChangedEvent(player.Id, Name(station), occupied: true));
            _events.LogInfo(LogSource, $"Player {player.Id} took {Name(station)}.");
            return true;
        }

        public bool Leave(Guid playerId)
        {
            if (!_byPlayer.TryGetValue(playerId, out var occupancy)) return false;
            _byPlayer.Remove(playerId);

            var player = occupancy.Player;
            bool playerAlive = player is not Component component || component != null;
            if (playerAlive)
            {
                foreach (var ability in occupancy.Granted) _abilities.RemoveAbility(player, ability);
                if (occupancy.Station.OccupyAbility != null) _abilities.CancelAbility(player, occupancy.Station.OccupyAbility);
            }

            if (IsAlive(occupancy.Station)) occupancy.Station.ServerSetOccupant(null);

            _events.Publish(new StationOccupancyChangedEvent(playerId, Name(occupancy.Station), occupied: false));
            _events.LogInfo(LogSource, $"Player {playerId} left {Name(occupancy.Station)}.");
            return true;
        }

        public IStation? StationOf(Guid playerId) => _byPlayer.TryGetValue(playerId, out var occupancy) ? occupancy.Station : null;

        public IHumanoidCharacterView? OccupantOf(IStation station) =>
            _byPlayer.Values.FirstOrDefault(occupancy => ReferenceEquals(occupancy.Station, station))?.Player;

        /// <summary>Interact while occupying means "leave"; it never reaches the interaction query.</summary>
        public bool TryHandleInteract(IHumanoidCharacterView player) => Leave(player.Id);

        public void Tick()
        {
            if (!_network.IsServer || _byPlayer.Count == 0) return;

            foreach (var occupancy in _byPlayer.Values.ToArray())
            {
                bool playerGone = !_actors.TryGetActor(occupancy.Player.Id, out _);
                if (playerGone || !IsAlive(occupancy.Station)) Leave(occupancy.Player.Id);
            }
        }

        private static bool IsAlive(IStation station) => station is not Component component || component != null;

        private static string Name(IStation station) =>
            station is Component component && component != null ? component.name : station.GetType().Name;
    }
}
