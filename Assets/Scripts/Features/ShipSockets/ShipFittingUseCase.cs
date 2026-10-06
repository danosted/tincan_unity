#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Sockets;
using Unity.Netcode;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Server, per frame: mounts fittings in ship sockets and keeps the replicated list true. A request is refused for an
    /// unknown fitting, a socket the ship does not have, a taken socket, or a player further than
    /// <see cref="ShipSocketsConfig.MaxReach"/> from it. A fitting whose socket goes (its part was removed) is despawned.
    /// Any fitting fits any socket for now. Plan: .docs/plans/modular-airship-builder.md (S5).
    /// </summary>
    public sealed class ShipFittingUseCase : ITickable, IShipFittings
    {
        private const string LogSource = "ShipFittings";

        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly IShipSockets _sockets;
        private readonly IShipFittingCatalog _catalog;
        private readonly IModuleSpawningService _spawning;
        private readonly ShipSocketsConfig _config;
        private readonly IEventPublisher _events;
        private readonly Dictionary<(Guid Ship, ShipSocketId Socket), (string FittingId, GameObject? Fixture)> _mounted = new();

        public ShipFittingUseCase(IActorRegistry actors, INetworkService network, IShipSockets sockets, IShipFittingCatalog catalog,
            IModuleSpawningService spawning, ShipSocketsConfig config, IEventPublisher events)
        {
            _actors = actors;
            _network = network;
            _sockets = sockets;
            _catalog = catalog;
            _spawning = spawning;
            _config = config;
            _events = events;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;

            foreach (var state in _actors.GetActors<IShipFittingState>().ToList())
            {
                var ship = state.Ship;
                if (ship == null) continue;

                bool changed = false;
                foreach (var request in state.TakeRequests())
                {
                    changed |= TryMount(ship, request.Socket, request.FittingId, PlayerPosition(request.ClientId), out var error);
                    if (error != null) _events.LogWarning(LogSource, $"Client {request.ClientId}: {error}");
                }

                changed |= Reconcile(ship.Id);
                if (changed) state.ServerSetMounted(MountedOn(ship.Id));
            }
        }

        public bool Mount(Guid shipId, ShipSocketId socket, string fittingId, out string? error)
        {
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(s => s.Id == shipId);
            if (ship == null)
            {
                error = "There is no such ship.";
                return false;
            }

            bool mounted = TryMount(ship, socket, fittingId, null, out error);
            if (mounted) Publish(shipId);
            return mounted;
        }

        public bool Unmount(Guid shipId, ShipSocketId socket)
        {
            if (!_mounted.TryGetValue((shipId, socket), out var entry)) return false;

            if (entry.Fixture != null) _spawning.DespawnModule(entry.Fixture);
            _mounted.Remove((shipId, socket));
            Publish(shipId);
            return true;
        }

        public IReadOnlyList<MountedFitting> MountedOn(Guid shipId) =>
            _mounted.Where(m => m.Key.Ship == shipId)
                .OrderBy(m => m.Key.Socket.PartInstanceId).ThenBy(m => m.Key.Socket.Index)
                .Select(m => new MountedFitting(m.Key.Socket, m.Value.FittingId, NetworkId(m.Value.Fixture)))
                .ToList();

        private bool TryMount(IAirshipView ship, ShipSocketId socketId, string fittingId, Vector3? requester, out string? error)
        {
            var socket = _sockets.SocketsOf(ship.Id).FirstOrDefault(s => s.Id.Equals(socketId));
            error = Refusal(ship.Id, socketId, socket, fittingId, requester);
            if (error != null) return false;

            _catalog.TryGet(fittingId, out var fitting);
            var fixture = _spawning.SpawnModule(fitting.Prefab!, socket.Mount.position, socket.Mount.rotation, ship);
            _mounted[(ship.Id, socketId)] = (fittingId, fixture);
            _events.Publish(new ShipFittingMountedEvent(ship.Id, socketId, fittingId));
            return true;
        }

        private string? Refusal(Guid shipId, ShipSocketId socketId, ShipSocketInfo socket, string fittingId, Vector3? requester)
        {
            if (!_network.IsServer) return "Only the server mounts fittings.";
            if (!_catalog.TryGet(fittingId, out _)) return $"There is no fitting \"{fittingId}\".";
            if (socket.Mount == null) return $"The ship has no {socketId}.";
            if (_mounted.ContainsKey((shipId, socketId))) return $"The {socketId} is taken.";
            if (requester is not { } position) return null;

            float distance = Vector3.Distance(position, socket.Mount.position);
            return distance > _config.MaxReach ? $"Too far from the {socketId} to mount anything ({distance:0.0} m)." : null;
        }

        /// <summary>Despawns fittings whose socket went with its part, and forgets fittings that were destroyed.</summary>
        private bool Reconcile(Guid shipId)
        {
            var sockets = new HashSet<ShipSocketId>(_sockets.SocketsOf(shipId).Where(s => s.Mount != null).Select(s => s.Id));
            bool changed = false;
            foreach (var key in _mounted.Keys.Where(k => k.Ship == shipId).ToList())
            {
                var fixture = _mounted[key].Fixture;
                bool socketGone = !sockets.Contains(key.Socket);
                if (!socketGone && fixture != null) continue;

                if (fixture != null) _spawning.DespawnModule(fixture);
                _mounted.Remove(key);
                changed = true;
            }

            return changed;
        }

        private void Publish(Guid shipId)
        {
            var state = _actors.GetActors<IShipFittingState>().FirstOrDefault(s => s.Ship?.Id == shipId);
            state?.ServerSetMounted(MountedOn(shipId));
        }

        private Vector3? PlayerPosition(ulong clientId)
        {
            foreach (var actor in _actors.GetActors<IHumanoidActor>())
            {
                if (actor is IPossessable possessable && possessable.OwnerId == clientId && actor is Component body && body != null)
                {
                    return body.transform.position;
                }
            }

            return null;
        }

        private static ulong NetworkId(GameObject? fixture) =>
            fixture != null && fixture.TryGetComponent<NetworkObject>(out var networkObject) ? networkObject.NetworkObjectId : 0;
    }
}
