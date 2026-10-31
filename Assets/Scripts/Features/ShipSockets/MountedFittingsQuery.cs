#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Ship.Sockets;
using UnityEngine;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Every peer: <see cref="IMountedFittings"/> from the replicated <see cref="IShipFittingState"/> and the sockets this
    /// peer built (<see cref="IShipSockets"/>). A fitting whose socket is not built here (yet) is left out.
    /// </summary>
    public sealed class MountedFittingsQuery : IMountedFittings
    {
        private readonly IActorRegistry _actors;
        private readonly IShipSockets _sockets;
        private readonly List<MountedFittingInfo> _results = new();

        public MountedFittingsQuery(IActorRegistry actors, IShipSockets sockets)
        {
            _actors = actors;
            _sockets = sockets;
        }

        public IReadOnlyList<MountedFittingInfo> FittingsOn(Guid shipId)
        {
            _results.Clear();
            var state = _actors.GetActors<IShipFittingState>().FirstOrDefault(candidate => candidate.IsSimulating && candidate.Ship?.Id == shipId);
            if (state == null) return _results;

            var sockets = _sockets.SocketsOf(shipId);
            foreach (var mounted in state.Mounted.OrderBy(m => m.PartInstanceId).ThenBy(m => m.SocketIndex))
            {
                var socket = mounted.Socket;
                Transform? mount = null;
                foreach (var info in sockets)
                {
                    if (!info.Id.Equals(socket)) continue;
                    mount = info.Mount;
                    break;
                }

                if (mount != null) _results.Add(new MountedFittingInfo(socket, mounted.FittingId.ToString(), mount));
            }

            return _results;
        }
    }
}
