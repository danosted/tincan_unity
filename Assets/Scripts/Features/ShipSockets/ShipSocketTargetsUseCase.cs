#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Targeting;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Sockets;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Every peer, per frame: gives each ship socket a <see cref="ShipSocketTarget"/> (a trigger at standing height,
    /// registered for targeting) so a player can press E on it, keeps it free or taken from the replicated mounts, and
    /// removes targets whose socket went with its part.
    /// </summary>
    public sealed class ShipSocketTargetsUseCase : ITickable, IDisposable
    {
        private readonly IActorRegistry _actors;
        private readonly IShipSockets _sockets;
        private readonly ITargetableRegistry _targetables;
        private readonly ShipSocketsConfig _config;
        private readonly Dictionary<(Guid Ship, ShipSocketId Socket), ShipSocketTarget> _targets = new();
        private readonly HashSet<(Guid, ShipSocketId)> _seen = new();

        public ShipSocketTargetsUseCase(IActorRegistry actors, IShipSockets sockets, ITargetableRegistry targetables, ShipSocketsConfig config)
        {
            _actors = actors;
            _sockets = sockets;
            _targetables = targetables;
            _config = config;
        }

        public IEnumerable<ShipSocketTarget> Targets => _targets.Values.Where(t => t != null);

        public void Tick()
        {
            if (_config.MountInteraction == null) return;

            _seen.Clear();
            var states = _actors.GetActors<IShipFittingState>().ToList();
            foreach (var ship in _actors.GetActors<IAirshipView>().ToList())
            {
                var taken = new HashSet<ShipSocketId>(
                    states.FirstOrDefault(s => s.Ship?.Id == ship.Id)?.Mounted.Select(m => m.Socket) ?? Enumerable.Empty<ShipSocketId>());
                foreach (var socket in _sockets.SocketsOf(ship.Id))
                {
                    if (socket.Mount == null) continue;
                    var key = (ship.Id, socket.Id);
                    _seen.Add(key);
                    if (!_targets.TryGetValue(key, out var target) || target == null)
                    {
                        target = Create(ship.Id, socket);
                        _targets[key] = target;
                    }

                    bool free = !taken.Contains(socket.Id);
                    target.IsFree = free;
                    if (target.TryGetComponent<Collider>(out var trigger)) trigger.enabled = free;
                }
            }

            foreach (var key in _targets.Keys.Where(k => !_seen.Contains(k)).ToList()) Remove(key);
        }

        public void Dispose()
        {
            foreach (var key in _targets.Keys.ToList()) Remove(key);
        }

        private ShipSocketTarget Create(Guid shipId, ShipSocketInfo socket)
        {
            var holder = new GameObject(ShipSocketTarget.ObjectName);
            holder.transform.SetParent(socket.Mount, false);
            var trigger = holder.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = _config.TargetSize;
            trigger.center = new Vector3(0f, _config.TargetSize.y * 0.5f, 0f);
            var target = holder.AddComponent<ShipSocketTarget>();
            target.Bind(shipId, socket.Id, _config.MountInteraction!);
            _targetables.Register(target);
            return target;
        }

        private void Remove((Guid, ShipSocketId) key)
        {
            var target = _targets[key];
            _targets.Remove(key);
            _targetables.Unregister(target);
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target.gameObject);
            else Object.DestroyImmediate(target.gameObject);
        }
    }
}
