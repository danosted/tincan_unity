#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Humanoid;
using TinCan.Features.Stations;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Station occupancy without abilities or seats: who sits where, and how often players took and left.</summary>
    public sealed class FakeStationOccupancy : IStationOccupancy
    {
        private readonly Dictionary<Guid, (IHumanoidCharacterView Player, IStation Station)> _byPlayer = new();

        public int Taken { get; private set; }
        public int Left { get; private set; }

        public bool TryOccupy(IHumanoidCharacterView player, IStation station)
        {
            if (_byPlayer.ContainsKey(player.Id) || OccupantOf(station) != null) return false;
            _byPlayer[player.Id] = (player, station);
            station.ServerSetOccupant(player);
            Taken++;
            return true;
        }

        public bool Leave(Guid playerId)
        {
            if (!_byPlayer.TryGetValue(playerId, out var occupancy)) return false;
            _byPlayer.Remove(playerId);
            occupancy.Station.ServerSetOccupant(null);
            Left++;
            return true;
        }

        public IStation? StationOf(Guid playerId) => _byPlayer.TryGetValue(playerId, out var occupancy) ? occupancy.Station : null;

        public IHumanoidCharacterView? OccupantOf(IStation station) =>
            _byPlayer.Values.FirstOrDefault(occupancy => ReferenceEquals(occupancy.Station, station)).Player;
    }
}
