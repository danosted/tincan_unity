#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Ship;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Application Layer, every peer, every frame: keeps the islands around the ship standing (only the server simulates
    /// the ship; every peer builds around it). The layout comes from the session (<see cref="ISessionLayout"/>: a
    /// voyage's seed, keeping its start and destination clear) or, before any voyage, from the config's seed, keeping the
    /// world origin clear. When the ship enters another cell or the layout
    /// changes, it works out the islands wanted within the stream radius of the middle of the ship's cell, removes the
    /// others at once and builds the new ones nearest first, a few per frame. No ship, no islands.
    /// Plan: .docs/plans/sky-islands.md.
    /// </summary>
    public class SkyIslandStreamingUseCase : ITickable, ISkyIslands, IDisposable
    {
        private readonly IActorRegistry _actors;
        private readonly SkyIslandLayoutProcessor _layout;
        private readonly ISkyIslandBuilder _builder;
        private readonly SkyIslandConfig _config;

        private readonly Dictionary<SkyIslandId, StandingIsland> _standing = new();
        private readonly List<SkyIslandSpec> _pending = new();
        private readonly List<SkyIslandSpec> _wanted = new();
        private readonly HashSet<SkyIslandId> _wantedIds = new();
        private readonly List<SkyIslandId> _leaving = new();
        private readonly List<Vector3> _keepClear = new();

        private LayoutKey? _key;
        private Vector2Int _cell;

        [Inject]
        public SkyIslandStreamingUseCase(IActorRegistry actors, SkyIslandLayoutProcessor layout, ISkyIslandBuilder builder,
            SkyIslandConfig config)
        {
            _actors = actors;
            _layout = layout;
            _builder = builder;
            _config = config;
        }

        public int Seed => _key?.Seed ?? 0;
        public int StandingCount => _standing.Count;
        public int Pending => _pending.Count;

        public IEnumerable<SkyIslandSpec> Standing
        {
            get
            {
                foreach (var standing in _standing.Values) yield return standing.Spec;
            }
        }

        public void Tick()
        {
            var ship = FirstShip();
            if (ship == null)
            {
                RemoveAll();
                return;
            }

            Vector3 focus = ship.Transform.position;
            var key = CurrentLayout();
            var cell = _layout.CellOf(focus, _config.CellSize);
            if (_key is not { } current || !current.Equals(key) || cell != _cell)
            {
                _key = key;
                _cell = cell;
                Refresh(key, focus);
            }

            BuildSome();
        }

        public void Dispose()
        {
            _standing.Clear();
            _pending.Clear();
            _builder.Clear();
        }

        private void Refresh(LayoutKey key, Vector3 focus)
        {
            _keepClear.Clear();
            _keepClear.Add(key.Origin);
            if (key.FromSession) _keepClear.Add(key.Destination);

            _wanted.Clear();
            _layout.Around(key.Seed, _layout.CellCentre(_cell, _config.CellSize), _config.StreamRadius, _config.LayoutRules,
                _keepClear, _wanted);

            _wantedIds.Clear();
            foreach (var island in _wanted) _wantedIds.Add(island.Id);

            _leaving.Clear();
            foreach (var id in _standing.Keys)
            {
                if (!_wantedIds.Contains(id)) _leaving.Add(id);
            }
            foreach (var id in _leaving)
            {
                _builder.Remove(_standing[id].Built);
                _standing.Remove(id);
            }

            _pending.Clear();
            foreach (var island in _wanted)
            {
                if (!_standing.ContainsKey(island.Id)) _pending.Add(island);
            }
            _pending.Sort((a, b) => LevelDistance(b.Top, focus).CompareTo(LevelDistance(a.Top, focus)));
        }

        /// <summary>Builds from the end of the list, where the nearest wait.</summary>
        private void BuildSome()
        {
            int budget = Mathf.Max(1, _config.MaxBuildsPerFrame);
            while (budget-- > 0 && _pending.Count > 0)
            {
                var island = _pending[_pending.Count - 1];
                _pending.RemoveAt(_pending.Count - 1);
                _standing[island.Id] = new StandingIsland(island, _builder.Build(island));
            }
        }

        private void RemoveAll()
        {
            if (_key == null && _standing.Count == 0 && _pending.Count == 0) return;
            foreach (var standing in _standing.Values) _builder.Remove(standing.Built);
            _standing.Clear();
            _pending.Clear();
            _key = null;
        }

        private LayoutKey CurrentLayout()
        {
            foreach (var session in _actors.GetActors<ISessionLayout>())
            {
                if (session.LayoutSeed != 0) return new LayoutKey(session.LayoutSeed, session.Origin, session.Destination, true);
            }
            return new LayoutKey(_config.WorldSeed, Vector3.zero, Vector3.zero, false);
        }

        /// <summary>Any live ship: only the server simulates it, but every peer needs islands around it.</summary>
        private IAirshipView? FirstShip()
        {
            foreach (var ship in _actors.GetActors<IAirshipView>())
            {
                if (ship.Transform != null) return ship;
            }
            return null;
        }

        private static float LevelDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private readonly struct StandingIsland
        {
            public readonly SkyIslandSpec Spec;
            public readonly object Built;

            public StandingIsland(SkyIslandSpec spec, object built)
            {
                Spec = spec;
                Built = built;
            }
        }

        private readonly struct LayoutKey : IEquatable<LayoutKey>
        {
            public readonly int Seed;
            public readonly Vector3 Origin;
            public readonly Vector3 Destination;
            public readonly bool FromSession;

            public LayoutKey(int seed, Vector3 origin, Vector3 destination, bool fromSession)
            {
                Seed = seed;
                Origin = origin;
                Destination = destination;
                FromSession = fromSession;
            }

            public bool Equals(LayoutKey other) =>
                Seed == other.Seed && Origin == other.Origin && Destination == other.Destination && FromSession == other.FromSession;

            public override bool Equals(object? obj) => obj is LayoutKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Seed, Origin, Destination, FromSession);
        }
    }
}
