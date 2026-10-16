#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Navigation;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Application Layer, the navigator's own peer, every frame: plotting the route at the chart table. While the local
    /// player sits at a chart table (its replicated occupant), the Navigator context's mouse moves a cursor over the
    /// chart (<see cref="ChartProjectionProcessor"/>), and:
    /// <list type="bullet">
    /// <item>Place on a waypoint picks it, and dragging moves it; on a point of interest it makes that the route's end;
    /// elsewhere it bends the nearest leg ahead with a new waypoint (<see cref="RouteEditProcessor"/>).</item>
    /// <item>Remove takes the waypoint under the cursor out; Altitude raises or lowers the picked one; Zoom scales the chart.</item>
    /// </list>
    /// Each change goes to the server as a whole route (<see cref="IRouteState.RequestRoute"/>), at most every
    /// <see cref="NavigationConfig.SendInterval"/> while a waypoint is dragged or raised. While editing, the navigator sees
    /// their own copy; otherwise the replicated route. The chart presenter draws <see cref="View"/>.
    /// </summary>
    public class ChartNavigatorUseCase : ITickable
    {
        // How long the navigator keeps showing their own copy after the last change, while the server catches up.
        private const float HoldWorkingSeconds = 0.6f;
        private const float ZoomStep = 1.25f;

        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly IInputReader _input;
        private readonly NavigatorInputContext _controls;
        private readonly NavigationConfig _config;
        private readonly ChartContents _chart;
        private readonly ChartProjectionProcessor _projection;
        private readonly RouteEditProcessor _edit;
        private readonly ITimeService _time;
        private readonly List<RouteWaypoint> _working = new();
        private readonly List<Vector2> _points = new();
        private readonly List<PointOfInterest> _visible = new();

        private IChartTable? _table;
        private Vector2 _cursor;
        private float _range;
        private int _selected = -1;
        private int _workingActive = -1;
        private bool _dragging;
        private bool _editing;
        private bool _dirty;
        private float _sinceSend;
        private float _holdWorking;

        public ChartNavigatorUseCase(IActorRegistry actors, INetworkService network, IInputReader input, NavigatorInputContext controls,
            NavigationConfig config, ChartContents chart, ChartProjectionProcessor projection, RouteEditProcessor edit, ITimeService time)
        {
            _actors = actors;
            _network = network;
            _input = input;
            _controls = controls;
            _config = config;
            _chart = chart;
            _projection = projection;
            _edit = edit;
            _time = time;
            _range = config.ChartRange;
        }

        /// <summary>The local navigator's chart: their table, cursor, zoom, pick and the route as they see it; null when not navigating.</summary>
        public ChartNavigatorView? View { get; private set; }

        public void Tick()
        {
            var table = OccupiedTable();
            if (!ReferenceEquals(table, _table)) SitDown(table);
            var ship = table?.Ship;
            var route = ship != null ? RouteOf(ship) : null;
            if (table == null || ship == null || ship.Transform == null || route == null)
            {
                View = null;
                return;
            }

            float dt = _time.DeltaTime;
            _sinceSend += dt;
            _holdWorking -= dt;
            if (!_editing && _holdWorking <= 0f) CopyReplicated(route);

            Vector3 shipPosition = ship.Transform.position;
            float heading = _projection.Heading(ship.Transform.rotation);
            MoveCursor();
            Zoom();
            ChartPoints(shipPosition, heading);

            if (_input.WasPressedThisFrame(_controls.Place)) Place(route, shipPosition, heading);
            else if (_input.WasPressedThisFrame(_controls.Remove)) Remove();

            Drag(shipPosition, heading);
            Raise(dt);
            Send(route);

            View = new ChartNavigatorView(table, _cursor, _range, _selected, _working.ToArray(), _workingActive);
        }

        private void SitDown(IChartTable? table)
        {
            _table = table;
            _cursor = new Vector2(0f, 0.3f);
            _range = _config.ChartRange;
            _selected = -1;
            _dragging = false;
            _editing = false;
            _dirty = false;
            _holdWorking = 0f;
            _working.Clear();
        }

        private void CopyReplicated(IRouteState route)
        {
            _working.Clear();
            for (int i = 0; i < route.Count; i++) _working.Add(route.Waypoint(i));
            _workingActive = route.ActiveIndex;
            if (_selected >= _working.Count) _selected = -1;
        }

        private void MoveCursor()
        {
            _cursor += _input.ReadVector2(_controls.Cursor) * _config.CursorSpeed;
            _cursor = _projection.ClampToChart(_cursor);
        }

        private void Zoom()
        {
            float zoom = _input.ReadAxis(_controls.Zoom);
            if (zoom == 0f) return;
            _range = Mathf.Clamp(zoom > 0f ? _range / ZoomStep : _range * ZoomStep, _config.ChartRangeLimits.x, _config.ChartRangeLimits.y);
        }

        private void ChartPoints(Vector3 shipPosition, float heading)
        {
            _points.Clear();
            foreach (var waypoint in _working) _points.Add(_projection.ToChart(waypoint.Position, shipPosition, heading, _range));
        }

        private void Place(IRouteState route, Vector3 shipPosition, float heading)
        {
            int hit = _edit.Nearest(_points, _cursor, _config.PickRadius);
            if (hit >= 0)
            {
                _selected = hit;
                _dragging = !_working[hit].IsPoi;
                _editing = true;
                return;
            }

            if (TryPickPointOfInterest(route, shipPosition, heading, out var point))
            {
                EndAt(point);
                return;
            }

            if (_working.Count >= _config.MaxWaypoints) return;

            var (index, along) = _edit.Insertion(_points, _workingActive, _cursor);
            float startAltitude = index == Mathf.Max(_workingActive, 0) || index == 0 ? shipPosition.y : _working[index - 1].Position.y;
            float endAltitude = index < _working.Count ? _working[index].Position.y : startAltitude;
            float altitude = Mathf.Lerp(startAltitude, endAltitude, along);
            _working.Insert(index, new RouteWaypoint(_projection.ToWorld(_cursor, altitude, shipPosition, heading, _range)));
            if (_workingActive < 0) _workingActive = 0;
            _selected = index;
            _dragging = true;
            _editing = true;
            _dirty = true;
        }

        private bool TryPickPointOfInterest(IRouteState route, Vector3 shipPosition, float heading, out PointOfInterest picked)
        {
            _visible.Clear();
            foreach (var point in _chart.PointsOfInterest())
            {
                if (ChartContents.IsVisible(point, route)) _visible.Add(point);
            }

            var marks = _visible.Select(point => _projection.ClampToChart(_projection.ToChart(point.Position, shipPosition, heading, _range))).ToArray();
            int hit = _edit.Nearest(marks, _cursor, _config.PickRadius);
            picked = hit >= 0 ? _visible[hit] : default;
            return hit >= 0;
        }

        /// <summary>The route ends at this point of interest: it replaces a point-of-interest end, or is added after a plain one.</summary>
        private void EndAt(PointOfInterest point)
        {
            var end = new RouteWaypoint(point.Position, point.Id);
            if (_working.Count > 0 && _working[^1].IsPoi) _working[^1] = end;
            else if (_working.Count < _config.MaxWaypoints) _working.Add(end);
            else return;

            if (_workingActive < 0) _workingActive = 0;
            _selected = _working.Count - 1;
            _dirty = true;
            _sinceSend = float.MaxValue;
        }

        private void Remove()
        {
            int hit = _edit.Nearest(_points, _cursor, _config.PickRadius);
            if (hit < 0) return;

            _working.RemoveAt(hit);
            _workingActive = _edit.ActiveAfterRemoval(_workingActive, hit, _working.Count);
            _selected = -1;
            _dragging = false;
            _dirty = true;
            _sinceSend = float.MaxValue;
        }

        private void Drag(Vector3 shipPosition, float heading)
        {
            if (!_dragging) return;
            if (!_input.IsPressed(_controls.Place))
            {
                _dragging = false;
                _sinceSend = float.MaxValue;
                return;
            }

            if (_selected < 0 || _selected >= _working.Count) return;
            var waypoint = _working[_selected];
            var moved = _projection.ToWorld(_cursor, waypoint.Position.y, shipPosition, heading, _range);
            if ((moved - waypoint.Position).sqrMagnitude < 0.01f) return;
            _working[_selected] = new RouteWaypoint(moved);
            _dirty = true;
        }

        private void Raise(float dt)
        {
            float altitude = _input.ReadAxis(_controls.Altitude);
            if (_selected < 0 || _selected >= _working.Count || _working[_selected].IsPoi) return;
            if (altitude == 0f)
            {
                if (_editing && !_dragging) _sinceSend = float.MaxValue;
                return;
            }

            var waypoint = _working[_selected];
            _working[_selected] = new RouteWaypoint(waypoint.Position + Vector3.up * (altitude * _config.AltitudeRate * dt));
            _editing = true;
            _dirty = true;
        }

        private void Send(IRouteState route)
        {
            if (!_dirty)
            {
                if (_editing && !_dragging && _input.ReadAxis(_controls.Altitude) == 0f) _editing = false;
                return;
            }
            if (_sinceSend < _config.SendInterval) return;

            route.RequestRoute(_working.ToArray(), _workingActive);
            _dirty = false;
            _sinceSend = 0f;
            _holdWorking = HoldWorkingSeconds;
        }

        private IChartTable? OccupiedTable()
        {
            if (!_network.IsClient) return null;
            return _actors.GetActors<IChartTable>().FirstOrDefault(table => table.OccupantClientId == _network.LocalClientId);
        }

        private IRouteState? RouteOf(IAirshipView ship) =>
            _actors.GetActors<IRouteState>().FirstOrDefault(route => ReferenceEquals(route.Ship, ship));
    }
}
