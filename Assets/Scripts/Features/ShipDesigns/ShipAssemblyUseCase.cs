#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using VContainer.Unity;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Every peer, per frame: builds each ship from the design its state replicates (<see cref="ShipHullAssembler"/>:
    /// plain children, diffed by instance id when the design changes, the on-board volume fitted). On the server,
    /// functional parts (the helm) are spawned once as networked fixtures at their design pose. Unknown parts are skipped.
    /// </summary>
    public sealed class ShipAssemblyUseCase : ITickable, IDisposable, IShipAssembly
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
            public readonly HashSet<int> SpawnedFunctional = new();
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

        public bool TryGetBuilt(Guid shipId, out ulong hash, out int builtParts)
        {
            hash = 0;
            builtParts = 0;
            if (!_built.TryGetValue(shipId, out var built)) return false;

            hash = built.Hash;
            builtParts = built.Hull.BuiltCount + built.SpawnedFunctional.Count;
            return true;
        }

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
                    built = new BuiltShip(ship, new ShipHullAssembler(_catalog, _builder, buildFunctionalParts: false));
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
            foreach (var (placement, part) in built.Hull.Apply(design, shipTransform))
            {
                if (!_network.IsServer || part.NetworkedPrefab == null || !built.SpawnedFunctional.Add(placement.InstanceId)) continue;

                var position = shipTransform.TransformPoint(ShipPartPose.LocalPosition(placement, part));
                var rotation = shipTransform.rotation * ShipPartPose.LocalRotation(placement);
                _spawning.SpawnModule(part.NetworkedPrefab, position, rotation, built.Ship);
            }

            int unknown = built.Hull.UnknownCount;
            string skipped = unknown > 0 ? $", {unknown} unknown part(s) skipped" : string.Empty;
            _events.LogInfo(LogSource, $"Ship {built.Ship.Id} built from \"{design.Name}\" ({design.Parts.Count} parts, " +
                                       $"hash {ShipDesignHash.ToText(built.Hash)}{skipped}).");
        }

        private void ForgetGoneShips()
        {
            if (_built.Count == 0) return;
            foreach (var id in _built.Keys.ToList())
            {
                if (_states.Any(s => s.Ship?.Id == id)) continue;

                _built[id].Hull.Clear();
                _built.Remove(id);
            }
        }
    }
}
