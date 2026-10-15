#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Features.ShipDesigns;
using VContainer.Unity;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Server, per frame: once a ship stands built from a design, mounts that design's loadout
    /// (<c>&lt;design key&gt;.fittings.json</c>, the key being the design's name as the store names its file) through
    /// <see cref="IShipFittings"/>, once per design the ship is given. Fittings the ship cannot take (an unknown fitting, a
    /// socket the design no longer has, a socket already taken) are skipped with a warning; the rest still mount.
    /// Plan: .docs/plans/ship-fitting-loadouts.md.
    /// </summary>
    public sealed class ShipFittingLoadoutUseCase : ITickable
    {
        private const string LogSource = "ShipFittings";

        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly IShipAssembly _assembly;
        private readonly IShipFittingLoadoutStore _store;
        private readonly IShipFittings _fittings;
        private readonly ShipDesignsConfig _designs;
        private readonly IEventPublisher _events;
        private readonly Dictionary<Guid, ulong> _loadedFor = new();

        public ShipFittingLoadoutUseCase(IActorRegistry actors, INetworkService network, IShipAssembly assembly,
            IShipFittingLoadoutStore store, IShipFittings fittings, ShipDesignsConfig designs, IEventPublisher events)
        {
            _actors = actors;
            _network = network;
            _assembly = assembly;
            _store = store;
            _fittings = fittings;
            _designs = designs;
            _events = events;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;

            foreach (var state in _actors.GetActors<IShipDesignState>().ToList())
            {
                var ship = state.Ship;
                if (ship == null || state.DesignHash == 0) continue;
                if (_loadedFor.TryGetValue(ship.Id, out var loaded) && loaded == state.DesignHash) continue;
                if (!_assembly.TryGetBuilt(ship.Id, out var built, out _) || built != state.DesignHash) continue;

                _loadedFor[ship.Id] = state.DesignHash;
                MountLoadout(ship.Id, state);
            }
        }

        private void MountLoadout(Guid shipId, IShipDesignState state)
        {
            var decoded = ShipDesignBinaryCodec.Decode(state.DesignBytes, _designs.Limits);
            if (!decoded.Succeeded) return;

            string key = ShipDesignKeys.FromName(decoded.Design!.Name);
            if (!_store.TryLoad(key, out var loadout, out var error))
            {
                _events.LogError(LogSource, $"Ship {shipId}: the loadout of \"{key}\" is not readable ({error}).");
                return;
            }

            if (loadout.Fittings.Count == 0) return;

            var present = _fittings.MountedOn(shipId).ToDictionary(m => m.Socket, m => m.FittingId.ToString());
            int mounted = 0;
            foreach (var fitting in loadout.Fittings)
            {
                if (present.TryGetValue(fitting.Socket, out var there) && there == fitting.FittingId)
                {
                    mounted++;
                    continue;
                }

                if (_fittings.Mount(shipId, fitting.Socket, fitting.FittingId, out var refusal)) mounted++;
                else _events.LogWarning(LogSource, $"Ship {shipId}, loadout of \"{key}\": {fitting} skipped ({refusal}).");
            }

            _events.LogInfo(LogSource, $"Ship {shipId}: {mounted} of {loadout.Fittings.Count} fitting(s) of \"{key}\" mounted.");
        }
    }
}
