#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// Server: who hangs on which leash point, how long each rope is, whether they reached the point, and which winches
    /// are reeling. Shared by <see cref="OverboardUseCase"/> (attaches and lands players) and <see cref="LeashRopeUseCase"/>
    /// (moves them); kept apart so the rope's movement step needs nothing that depends on movement.
    /// </summary>
    public sealed class LeashStore
    {
        public sealed class Leash
        {
            public ILeashPoint Point = null!;
            public float Length;
            public bool Arrived;
        }

        private readonly Dictionary<Guid, Leash> _leashes = new();
        private readonly HashSet<Guid> _winching = new();

        public void Attach(Guid playerId, ILeashPoint point, float length) =>
            _leashes[playerId] = new Leash { Point = point, Length = length };

        public void Detach(Guid playerId) => _leashes.Remove(playerId);

        public bool TryGet(Guid playerId, out Leash leash) => _leashes.TryGetValue(playerId, out leash!);

        public bool IsWinching(ILeashPoint point) => _winching.Contains(point.Id);

        /// <summary>Starts or stops reeling at <paramref name="point"/>; returns whether it now reels.</summary>
        public bool ToggleWinch(ILeashPoint point)
        {
            if (_winching.Remove(point.Id)) return false;
            _winching.Add(point.Id);
            return true;
        }

        public bool AnyOn(ILeashPoint point)
        {
            foreach (var leash in _leashes.Values)
            {
                if (ReferenceEquals(leash.Point, point)) return true;
            }

            return false;
        }

        public void StopWinch(ILeashPoint point) => _winching.Remove(point.Id);
    }
}
