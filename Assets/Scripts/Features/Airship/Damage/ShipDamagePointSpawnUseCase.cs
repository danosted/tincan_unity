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

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Server, every frame: gives a ship built from a design its damage points, since such a ship has no fixed
    /// damage-sockets fixture. Hull spots come from its planked floors (<see cref="ShipDamageSpotProcessor"/>), and every
    /// mounted fitting gets a point beside it, despawned when the fitting goes. Points are networked children of the ship
    /// (<see cref="ShipDamageConfig.DesignedShipPointPrefab"/>), so <see cref="ShipDamageLocator"/>, breakage, repair and
    /// the HUD work on them unchanged. Indexes: hull spots 0..N-1, then fittings in socket order. A ship that already
    /// has fixed points (the old airship) is left alone. Reads only core contracts (<see cref="IShipHullSurfaces"/>,
    /// <see cref="IMountedFittings"/>), so it does nothing in a profile without ship designs or sockets.
    /// </summary>
    public sealed class ShipDamagePointSpawnUseCase : ITickable
    {
        private const string LogSource = "ShipDamage";

        // A hull point sits this far off its face, so its marker is not buried in the planks.
        private const float SurfaceLift = 0.05f;

        private sealed class ShipPoints
        {
            public int SurfacesVersion = -1;
            public readonly List<(Vector3 Local, GameObject Point)> Hull = new();
            public readonly Dictionary<ShipSocketId, GameObject> Fittings = new();
            public bool Dirty;
        }

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IModuleSpawningService _spawning;
        private readonly ShipDamageSpotProcessor _spots;
        private readonly ShipDamageConfig _config;
        private readonly IEventPublisher _events;
        private readonly IShipHullSurfaces? _surfaces;
        private readonly IMountedFittings? _fittings;
        private readonly Dictionary<Guid, ShipPoints> _ships = new();
        private readonly HashSet<Guid> _fixedShips = new();
        private readonly HashSet<ShipSocketId> _mounted = new();
        private readonly List<IAirshipView> _current = new();

        public ShipDamagePointSpawnUseCase(INetworkService network, IActorRegistry actors, IModuleSpawningService spawning,
            ShipDamageSpotProcessor spots, ShipDamageConfig config, IEventPublisher events,
            IEnumerable<IShipHullSurfaces> surfaces, IEnumerable<IMountedFittings> fittings)
        {
            _network = network;
            _actors = actors;
            _spawning = spawning;
            _spots = spots;
            _config = config;
            _events = events;
            _surfaces = surfaces.FirstOrDefault();
            _fittings = fittings.FirstOrDefault();
        }

        public void Tick()
        {
            if (!_network.IsServer || _surfaces == null || _config.DesignedShipPointPrefab == null) return;

            _current.Clear();
            _current.AddRange(_actors.GetActors<IAirshipView>().Where(ship => ship.IsSimulating && ship.Transform != null));
            foreach (var ship in _current)
            {
                var surfaces = _surfaces.SurfacesOf(ship.Id);
                if (surfaces.Count == 0 || _fixedShips.Contains(ship.Id)) continue;

                if (!_ships.TryGetValue(ship.Id, out var points))
                {
                    if (ShipDamageLocator.FindPoints(ship).Count > 0)
                    {
                        _fixedShips.Add(ship.Id);
                        continue;
                    }

                    _ships[ship.Id] = points = new ShipPoints();
                }

                SyncHull(ship, points, surfaces);
                SyncFittings(ship, points);
                if (!points.Dirty) continue;

                Assign(points);
                points.Dirty = false;
                _events.LogInfo(LogSource, $"Ship {ship.Id}: {points.Hull.Count} hull point(s), {points.Fittings.Count} fitting point(s).");
            }

            foreach (var gone in _ships.Keys.Where(id => _current.All(ship => ship.Id != id)).ToList()) _ships.Remove(gone);
        }

        /// <summary>When the ship's surfaces change (a rebuild), its hull spots are picked again; unchanged spots stay.</summary>
        private void SyncHull(IAirshipView ship, ShipPoints points, IReadOnlyList<ShipHullSurface> surfaces)
        {
            if (points.SurfacesVersion == _surfaces!.Version && points.Hull.All(hull => hull.Point != null)) return;
            points.SurfacesVersion = _surfaces.Version;

            var wanted = _spots.Pick(surfaces, _config.DesignedShipPoints)
                .Select(spot => spot.LocalPoint + spot.LocalNormal * SurfaceLift)
                .ToList();
            if (wanted.SequenceEqual(points.Hull.Where(hull => hull.Point != null).Select(hull => hull.Local))) return;

            foreach (var (_, point) in points.Hull) Despawn(point);
            points.Hull.Clear();

            var shipTransform = ship.Transform;
            foreach (var local in wanted)
            {
                var point = _spawning.SpawnModule(_config.DesignedShipPointPrefab!, shipTransform.TransformPoint(local), shipTransform.rotation, ship);
                if (point != null) points.Hull.Add((local, point));
            }

            points.Dirty = true;
        }

        /// <summary>One point beside each mounted fitting; a fitting's point goes when the fitting does.</summary>
        private void SyncFittings(IAirshipView ship, ShipPoints points)
        {
            _mounted.Clear();
            var mounted = _fittings?.FittingsOn(ship.Id) ?? Array.Empty<MountedFittingInfo>();
            foreach (var fitting in mounted) _mounted.Add(fitting.Socket);

            foreach (var (socket, point) in points.Fittings.ToList())
            {
                if (_mounted.Contains(socket) && point != null) continue;
                Despawn(point);
                points.Fittings.Remove(socket);
                points.Dirty = true;
            }

            var shipTransform = ship.Transform;
            foreach (var fitting in mounted)
            {
                if (points.Fittings.ContainsKey(fitting.Socket) || fitting.Mount == null) continue;

                var at = fitting.Mount.position + shipTransform.up * _config.FittingPointHeight;
                var point = _spawning.SpawnModule(_config.DesignedShipPointPrefab!, at, shipTransform.rotation, ship);
                if (point == null) continue;

                points.Fittings[fitting.Socket] = point;
                points.Dirty = true;
            }
        }

        private static void Assign(ShipPoints points)
        {
            int index = 0;
            foreach (var (_, point) in points.Hull) point.GetComponent<IDesignedDamagePoint>()?.ServerAssign(index++, null);
            foreach (var (socket, point) in points.Fittings.OrderBy(pair => pair.Key.PartInstanceId).ThenBy(pair => pair.Key.Index))
            {
                point.GetComponent<IDesignedDamagePoint>()?.ServerAssign(index++, socket);
            }
        }

        private void Despawn(GameObject? point)
        {
            if (point != null) _spawning.DespawnModule(point);
        }
    }
}
