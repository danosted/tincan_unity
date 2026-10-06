#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Targeting;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.ShipSockets;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>
    /// Fakes for ship socket tests. This plain class matches the file name on purpose, so Unity binds the file to it and
    /// the MonoBehaviour <see cref="FakeFittingPlayer"/> below can be added with AddComponent (see FakeShipDamage.cs).
    /// </summary>
    public static class FakeShipSockets
    {
        /// <summary>A socket marker under the ship at a ship-local position.</summary>
        public static ShipSocketInfo Socket(Transform ship, int partInstanceId, Vector3 localPosition, int index = 0)
        {
            var mount = new GameObject($"ShipSocket_{partInstanceId}_{index}").transform;
            mount.SetParent(ship, false);
            mount.localPosition = localPosition;
            return new ShipSocketInfo(new ShipSocketId(partInstanceId, index), mount);
        }

        public static FakeFittingPlayer Player(Vector3 position, ulong clientId)
        {
            var body = new GameObject($"Player_{clientId}");
            body.transform.position = position;
            var player = body.AddComponent<FakeFittingPlayer>();
            player.ClientId = clientId;
            return player;
        }
    }

    /// <summary>The sockets of each ship, set by the test.</summary>
    public sealed class FakeShipSocketList : IShipSockets
    {
        public Dictionary<Guid, List<ShipSocketInfo>> Ships { get; } = new();
        public int Version { get; set; }

        public IReadOnlyList<ShipSocketInfo> SocketsOf(Guid shipId) =>
            Ships.TryGetValue(shipId, out var sockets) ? sockets : (IReadOnlyList<ShipSocketInfo>)Array.Empty<ShipSocketInfo>();
    }

    /// <summary>A ship's fitting state without NGO: records chooser requests; mount requests are queued by the test.</summary>
    public sealed class FakeShipFittingState : IShipFittingState
    {
        private ShipSocketId? _chooser;

        public FakeShipFittingState(IAirshipView ship) => Ship = ship;

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public IAirshipView? Ship { get; }
        public IReadOnlyList<MountedFitting> Mounted { get; private set; } = Array.Empty<MountedFitting>();
        public List<MountRequest> Requests { get; } = new();
        public List<(ulong ClientId, ShipSocketId Socket)> ChoosersOpened { get; } = new();
        public List<(ShipSocketId Socket, string FittingId)> Sent { get; } = new();

        public void ServerSetMounted(IReadOnlyList<MountedFitting> mounted) => Mounted = mounted.ToList();

        public void ServerOpenChooser(ulong clientId, ShipSocketId socket)
        {
            ChoosersOpened.Add((clientId, socket));
            _chooser = socket;
        }

        public bool TryTakeChooser(out ShipSocketId socket)
        {
            socket = _chooser ?? default;
            if (_chooser == null) return false;
            _chooser = null;
            return true;
        }

        public void RequestMount(ShipSocketId socket, string fittingId) => Sent.Add((socket, fittingId));

        public IReadOnlyList<MountRequest> TakeRequests()
        {
            var taken = Requests.ToList();
            Requests.Clear();
            return taken;
        }
    }

    public sealed class FakeTargetableRegistry : ITargetableRegistry
    {
        private readonly List<ITargetable> _all = new();
        public IReadOnlyCollection<ITargetable> All => _all;
        public void Register(ITargetable targetable) => _all.Add(targetable);
        public void Unregister(ITargetable targetable) => _all.Remove(targetable);
    }

    /// <summary>A player's body: a humanoid a client owns, standing somewhere.</summary>
    public sealed class FakeFittingPlayer : MonoBehaviour, IHumanoidActor, IPossessable
    {
        public ulong ClientId { get; set; }
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public bool IsPlayerCharacter => true;
        public ulong? PossessorId => ClientId;
        public ulong? OwnerId => ClientId;
        public void AuthoritativeSetPossessor(ulong? playerId) { }
        public bool CanPossess(ulong playerId) => true;
    }
}
