#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Navigation;
using TinCan.Core.Ship;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Presentation, every peer, every frame: the chart on every chart table, drawn in 3D on its table top. Heading-up and
    /// centred on the ship (<see cref="ChartProjectionProcessor"/>):
    /// <list type="bullet">
    /// <item>the ship, the route ahead as pins on stems joined by a line (the active leg brighter), each pin raised by its
    /// height above the ship;</item>
    /// <item>the points of interest the crew can see (faraway ones at the rim);</item>
    /// <item>obstacles as columns spanning the altitude they block (rock or hazard);</item>
    /// <item>at the local navigator's table, their cursor, zoom, pick and the route as they are editing it.</item>
    /// </list>
    /// Built from replicated and seeded state only; crewmates see the same chart from the deck.
    /// </summary>
    public sealed class ChartPresenter : ITickable, IDisposable
    {
        // Heights over the table top, in table metres.
        private const float Lift = 0.05f;
        private const float LowestMark = 0.008f;
        private const float HighestMark = 0.3f;

        private readonly IActorRegistry _actors;
        private readonly NavigationConfig _config;
        private readonly ChartContents _chart;
        private readonly ChartProjectionProcessor _projection;
        private readonly ChartNavigatorUseCase _navigator;
        private readonly Dictionary<IChartTable, ChartMarks> _marks = new();
        private readonly List<IChartTable> _gone = new();
        private readonly List<Vector3> _line = new();

        public ChartPresenter(IActorRegistry actors, NavigationConfig config, ChartContents chart, ChartProjectionProcessor projection,
            ChartNavigatorUseCase navigator)
        {
            _actors = actors;
            _config = config;
            _chart = chart;
            _projection = projection;
            _navigator = navigator;
        }

        /// <summary>How many marks of each kind the chart at this table shows now (scenarios read it).</summary>
        public (int Waypoints, int Points, int Rocks, int Hazards) Counts(IChartTable table) =>
            _marks.TryGetValue(table, out var marks) ? (marks.Waypoints, marks.Points, marks.Rocks, marks.Hazards) : default;

        public void Tick()
        {
            RemoveGone();
            foreach (var table in _actors.GetActors<IChartTable>())
            {
                if (table is not Component component || component == null || table.Chart == null) continue;
                var ship = table.Ship;
                var route = ship != null ? _actors.GetActors<IRouteState>().FirstOrDefault(candidate => ReferenceEquals(candidate.Ship, ship)) : null;
                if (ship == null || ship.Transform == null || route == null) continue;

                if (!_marks.TryGetValue(table, out var marks)) _marks[table] = marks = new ChartMarks(table.Chart, _config);
                var view = _navigator.View is { } local && ReferenceEquals(local.Table, table) ? local : null;
                Draw(marks, ship, route, view);
            }
        }

        public void Dispose()
        {
            foreach (var marks in _marks.Values) marks.Destroy();
            _marks.Clear();
        }

        private void Draw(ChartMarks marks, IAirshipView ship, IRouteState route, ChartNavigatorView? view)
        {
            Vector3 shipPosition = ship.Transform.position;
            float heading = _projection.Heading(ship.Transform.rotation);
            float range = view?.Range ?? _config.ChartRange;
            float radius = _config.ChartRadius;
            marks.Begin();

            marks.Ship(new Vector3(0f, Lift, 0f));

            foreach (var obstacle in _chart.Obstacles(shipPosition, range))
            {
                Vector2 at = _projection.ToChart(obstacle.Center, shipPosition, heading, range);
                float size = obstacle.Radius / range;
                if (at.magnitude - size > 1f) continue;

                float bottom = Height(obstacle.Bottom, shipPosition.y);
                float top = Mathf.Max(Height(obstacle.Top, shipPosition.y), bottom + 0.004f);
                marks.Obstacle(new Vector3(at.x * radius, (bottom + top) * 0.5f, at.y * radius), size * radius, top - bottom, obstacle.Kind);
            }

            foreach (var point in _chart.PointsOfInterest())
            {
                if (!ChartContents.IsVisible(point, route)) continue;
                Vector2 at = _projection.ClampToChart(_projection.ToChart(point.Position, shipPosition, heading, range));
                marks.Point(new Vector3(at.x * radius, Height(point.Position.y, shipPosition.y), at.y * radius));
            }

            int count = view?.Waypoints.Count ?? route.Count;
            int active = view?.Active ?? route.ActiveIndex;
            _line.Clear();
            _line.Add(new Vector3(0f, Lift, 0f));
            for (int i = Mathf.Max(active, 0); i < count; i++)
            {
                var waypoint = view != null ? view.Waypoints[i] : route.Waypoint(i);
                Vector2 at = _projection.ClampToChart(_projection.ToChart(waypoint.Position, shipPosition, heading, range));
                var head = new Vector3(at.x * radius, Height(waypoint.Position.y, shipPosition.y), at.y * radius);
                marks.Waypoint(head, selected: view != null && view.Selected == i, poi: waypoint.IsPoi);
                _line.Add(head);
            }
            marks.Route(_line);

            if (view != null) marks.Cursor(new Vector3(view.Cursor.x * radius, Lift, view.Cursor.y * radius));
            marks.End();
        }

        /// <summary>A mark's height over the table for something at <paramref name="altitude"/> (the ship's altitude sits at <see cref="Lift"/>).</summary>
        private float Height(float altitude, float shipAltitude) =>
            Mathf.Clamp(Lift + (altitude - shipAltitude) * _config.AltitudeScale, LowestMark, HighestMark);

        private void RemoveGone()
        {
            _gone.Clear();
            foreach (var pair in _marks)
            {
                if (pair.Key is not Component component || component == null) _gone.Add(pair.Key);
            }
            foreach (var table in _gone)
            {
                _marks[table].Destroy();
                _marks.Remove(table);
            }
        }

        /// <summary>The pooled marks on one table; only the ones used this frame are shown.</summary>
        private sealed class ChartMarks
        {
            private const float StemWidth = 0.006f;

            private readonly Transform _root;
            private readonly Pool _ships;
            private readonly Pool _pins;
            private readonly Pool _stems;
            private readonly Pool _points;
            private readonly Pool _rocks;
            private readonly Pool _hazards;
            private readonly Pool _cursor;
            private readonly LineRenderer _route;
            private readonly LineRenderer _activeLeg;

            public ChartMarks(Transform chart, NavigationConfig config)
            {
                _root = new GameObject("ChartMarks").transform;
                _root.SetParent(chart, false);
                _ships = new Pool(_root, PrimitiveType.Cube, config.ShipMarkMaterial);
                _pins = new Pool(_root, PrimitiveType.Sphere, config.RouteMaterial);
                _stems = new Pool(_root, PrimitiveType.Cylinder, config.RouteMaterial);
                _points = new Pool(_root, PrimitiveType.Cube, config.PoiMaterial);
                _rocks = new Pool(_root, PrimitiveType.Cylinder, config.RockMaterial);
                _hazards = new Pool(_root, PrimitiveType.Cylinder, config.HazardMaterial);
                _cursor = new Pool(_root, PrimitiveType.Cylinder, config.CursorMaterial);
                _route = Line("Route", config.RouteMaterial, 0.008f);
                _activeLeg = Line("ActiveLeg", config.ActiveLegMaterial, 0.016f);
            }

            public int Waypoints => _pins.Used;
            public int Points => _points.Used;
            public int Rocks => _rocks.Used;
            public int Hazards => _hazards.Used;

            public void Begin()
            {
                _ships.Begin();
                _pins.Begin();
                _stems.Begin();
                _points.Begin();
                _rocks.Begin();
                _hazards.Begin();
                _cursor.Begin();
            }

            public void End()
            {
                _ships.End();
                _pins.End();
                _stems.End();
                _points.End();
                _rocks.End();
                _hazards.End();
                _cursor.End();
            }

            public void Ship(Vector3 at) => _ships.Next(at, Quaternion.identity, new Vector3(0.035f, 0.016f, 0.08f));

            public void Waypoint(Vector3 head, bool selected, bool poi)
            {
                float size = selected ? 0.06f : poi ? 0.045f : 0.04f;
                _pins.Next(head, Quaternion.identity, Vector3.one * size);
                Stem(head);
            }

            public void Point(Vector3 at)
            {
                _points.Next(at, Quaternion.Euler(45f, 45f, 0f), Vector3.one * 0.04f);
                Stem(at);
            }

            public void Obstacle(Vector3 center, float radius, float height, ChartObstacleKind kind)
            {
                var pool = kind == ChartObstacleKind.Hazard ? _hazards : _rocks;
                pool.Next(center, Quaternion.identity, new Vector3(radius * 2f, height * 0.5f, radius * 2f));
            }

            public void Cursor(Vector3 at)
            {
                _cursor.Next(at, Quaternion.identity, new Vector3(0.07f, 0.002f, 0.07f));
                _cursor.Next(new Vector3(at.x, at.y * 0.5f, at.z), Quaternion.identity, new Vector3(0.004f, at.y * 0.5f, 0.004f));
            }

            public void Route(List<Vector3> line)
            {
                _route.positionCount = line.Count;
                for (int i = 0; i < line.Count; i++) _route.SetPosition(i, line[i]);
                _activeLeg.positionCount = line.Count >= 2 ? 2 : 0;
                if (line.Count >= 2)
                {
                    _activeLeg.SetPosition(0, line[0]);
                    _activeLeg.SetPosition(1, line[1]);
                }
            }

            public void Destroy()
            {
                if (_root != null) Object.Destroy(_root.gameObject);
            }

            private void Stem(Vector3 head)
            {
                float height = Mathf.Max(head.y, 0.001f);
                _stems.Next(new Vector3(head.x, height * 0.5f, head.z), Quaternion.identity, new Vector3(StemWidth, height * 0.5f, StemWidth));
            }

            private LineRenderer Line(string name, Material? material, float width)
            {
                var line = new GameObject(name).AddComponent<LineRenderer>();
                line.transform.SetParent(_root, false);
                line.useWorldSpace = false;
                line.widthMultiplier = width;
                line.positionCount = 0;
                line.shadowCastingMode = ShadowCastingMode.Off;
                if (material != null) line.sharedMaterial = material;
                return line;
            }
        }

        /// <summary>Primitives of one kind and material, reused frame to frame.</summary>
        private sealed class Pool
        {
            private readonly Transform _parent;
            private readonly PrimitiveType _shape;
            private readonly Material? _material;
            private readonly List<Transform> _items = new();

            public Pool(Transform parent, PrimitiveType shape, Material? material)
            {
                _parent = parent;
                _shape = shape;
                _material = material;
            }

            public int Used { get; private set; }

            public void Begin() => Used = 0;

            public void Next(Vector3 localPosition, Quaternion localRotation, Vector3 scale)
            {
                if (Used == _items.Count) _items.Add(Create());
                var item = _items[Used++];
                item.localPosition = localPosition;
                item.localRotation = localRotation;
                item.localScale = scale;
                if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
            }

            public void End()
            {
                for (int i = Used; i < _items.Count; i++)
                {
                    if (_items[i].gameObject.activeSelf) _items[i].gameObject.SetActive(false);
                }
            }

            private Transform Create()
            {
                var item = GameObject.CreatePrimitive(_shape);
                Object.Destroy(item.GetComponent<Collider>());
                var renderer = item.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (_material != null) renderer.sharedMaterial = _material;
                item.transform.SetParent(_parent, false);
                return item.transform;
            }
        }
    }
}
