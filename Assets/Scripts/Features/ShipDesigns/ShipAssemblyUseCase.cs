#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Sockets;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Every peer, per frame: builds each ship from the design its state replicates (<see cref="ShipHullAssembler"/>:
    /// plain children, diffed by instance id when the design changes, the on-board volume fitted). On the server,
    /// functional parts (the helm) are spawned once as networked fixtures at their design pose; when the design changes,
    /// those the new design no longer has, or has moved, turned or swapped, are despawned and the new ones spawned.
    /// Unknown parts are skipped.
    /// </summary>
    public sealed class ShipAssemblyUseCase : ITickable, IDisposable, IShipAssembly, IShipSockets
    {
        private const string LogSource = "ShipAssembly";

        private sealed class BuiltShip
        {
            public BuiltShip(IAirshipView ship, ShipHullAssembler hull)
            {
                Ship = ship;
                Hull = hull;
            }

            public IAirshipView Ship { get; }
            public ShipHullAssembler Hull { get; }
            public ulong Hash;

            /// <summary>Server: the functional parts spawned for the ship, by instance id, with the placement they stand for.</summary>
            public readonly Dictionary<int, (string PartId, Vector3 Position, Quaternion Rotation, GameObject? Module)> SpawnedFunctional = new();
        }

        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly IShipPartCatalog _catalog;
        private readonly ShipDesignsConfig _config;
        private readonly IShipHullBuilder _builder;
        private readonly IModuleSpawningService _spawning;
        private readonly IEventPublisher _events;
        private readonly Dictionary<Guid, BuiltShip> _built = new();
        private readonly List<IShipDesignState> _states = new();
        private readonly Dictionary<Guid, List<ShipSocketInfo>> _sockets = new();
        private static readonly IReadOnlyList<ShipSocketInfo> NoSockets = Array.Empty<ShipSocketInfo>();

        public ShipAssemblyUseCase(IActorRegistry actors, INetworkService network, IShipPartCatalog catalog, ShipDesignsConfig config,
            IShipHullBuilder builder, IModuleSpawningService spawning, IEventPublisher events)
        {
            _actors = actors;
            _network = network;
            _catalog = catalog;
            _config = config;
            _builder = builder;
            _spawning = spawning;
            _events = events;
        }

        public int Version { get; private set; }

        public IReadOnlyList<ShipSocketInfo> SocketsOf(Guid shipId) => _sockets.TryGetValue(shipId, out var sockets) ? sockets : NoSockets;

        public bool TryGetBuilt(Guid shipId, out ulong hash, out int builtParts)
        {
            hash = 0;
            builtParts = 0;
            if (!_built.TryGetValue(shipId, out var built)) return false;

            hash = built.Hash;
            builtParts = built.Hull.BuiltCount + built.SpawnedFunctional.Count;
            return true;
        }

        public int BuiltFrameMeshes(Guid shipId) => _built.TryGetValue(shipId, out var built) ? built.Hull.BuiltFrameMeshes : 0;

        public void Tick()
        {
            _states.Clear();
            _states.AddRange(_actors.GetActors<IShipDesignState>());

            foreach (var state in _states)
            {
                var ship = state.Ship;
                if (ship == null || ship.Transform == null || state.DesignHash == 0) continue;

                if (!_built.TryGetValue(ship.Id, out var built))
                {
                    built = new BuiltShip(ship, new ShipHullAssembler(_catalog, _builder, buildFunctionalParts: false, _config));
                    _built.Add(ship.Id, built);
                }

                if (built.Hash == state.DesignHash) continue;
                built.Hash = state.DesignHash;

                var decoded = ShipDesignBinaryCodec.Decode(state.DesignBytes, _config.Limits);
                if (!decoded.Succeeded)
                {
                    _events.LogError(LogSource, $"Ship {ship.Id}: the replicated design is not readable ({decoded.Error}).");
                    continue;
                }

                Assemble(built, decoded.Design!);
            }

            ForgetGoneShips();
        }

        public void Dispose()
        {
            foreach (var built in _built.Values) built.Hull.Clear();
            _built.Clear();
        }

        private void Assemble(BuiltShip built, ShipDesign design)
        {
            var shipTransform = built.Ship.Transform;
            built.Hull.Apply(design, shipTransform);
            if (_network.IsServer) SpawnFunctionalParts(built, design);

            CollectSockets(built);

            int unknown = built.Hull.UnknownCount;
            string skipped = unknown > 0 ? $", {unknown} unknown part(s) skipped" : string.Empty;
            _events.LogInfo(LogSource, $"Ship {built.Ship.Id} built from \"{design.Name}\" ({design.Parts.Count} parts, " +
                                       $"hash {ShipDesignHash.ToText(built.Hash)}{skipped}).");
        }

        /// <summary>
        /// Server: the ship's spawned functional parts match the design's. A part is kept only while the design still has
        /// it, unchanged (same part, same pose: its anchor and the frame under it); anything else is despawned, and every
        /// functional part not standing is spawned at its design pose. A part its anchor does not hold is not spawned.
        /// </summary>
        private void SpawnFunctionalParts(BuiltShip built, ShipDesign design)
        {
            var shape = ShipFrameShape.From(design.Frame);
            var wanted = new Dictionary<int, (string PartId, Vector3 Position, Quaternion Rotation, GameObject Prefab)>();
            foreach (var placement in design.Parts)
            {
                if (!_catalog.TryGet(placement.PartId, out var part) || part.NetworkedPrefab == null) continue;
                if (ShipPartPose.TryGetPose(placement, part, shape, out var position, out var rotation))
                {
                    wanted[placement.InstanceId] = (placement.PartId, position, rotation, part.NetworkedPrefab);
                }
            }

            foreach (var (id, spawned) in built.SpawnedFunctional.ToList())
            {
                if (wanted.TryGetValue(id, out var keep) && keep.PartId == spawned.PartId && keep.Position == spawned.Position && keep.Rotation == spawned.Rotation) continue;

                if (spawned.Module != null) _spawning.DespawnModule(spawned.Module);
                built.SpawnedFunctional.Remove(id);
            }

            var shipTransform = built.Ship.Transform;
            foreach (var (id, (partId, position, rotation, prefab)) in wanted)
            {
                if (built.SpawnedFunctional.ContainsKey(id)) continue;

                var module = _spawning.SpawnModule(prefab, shipTransform.TransformPoint(position), shipTransform.rotation * rotation, built.Ship);
                built.SpawnedFunctional[id] = (partId, position, rotation, module);
            }
        }

        /// <summary>The sockets of the parts built for a ship: index i is the i-th ShipSocket under the part.</summary>
        private void CollectSockets(BuiltShip built)
        {
            var sockets = new List<ShipSocketInfo>();
            foreach (var (placement, handle) in built.Hull.Built.OrderBy(b => b.Placement.InstanceId))
            {
                if (handle is not GameObject part || part == null) continue;
                var markers = part.GetComponentsInChildren<ShipSocket>(true);
                for (int i = 0; i < markers.Length; i++) sockets.Add(new ShipSocketInfo(new ShipSocketId(placement.InstanceId, i), markers[i].transform));
            }

            _sockets[built.Ship.Id] = sockets;
            Version++;
        }

        private void ForgetGoneShips()
        {
            if (_built.Count == 0) return;
            foreach (var id in _built.Keys.ToList())
            {
                if (_states.Any(s => s.Ship?.Id == id)) continue;

                _built[id].Hull.Clear();
                _built.Remove(id);
                if (_sockets.Remove(id)) Version++;
            }
        }
    }
}
