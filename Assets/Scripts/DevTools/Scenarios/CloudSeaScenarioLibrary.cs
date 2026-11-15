#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Ship;
using TinCan.Features.CloudBoundary;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the drawn cloud sea and its holes (.docs/plans/cloud-sea.md, "The drawn sea and its holes").
    /// Command: OpenScenarioHole (server: opens a hole of a radius no ambient hole has, beside the first ship). Probe:
    /// ScenarioHoleOpen: on this peer that hole is fully open, the cloud surface at its centre has dropped by the hole
    /// depth below the swell there, and, where this peer has a view, the sea shader has been handed the holes.
    /// </summary>
    public sealed class CloudSeaScenarioLibrary : IScenarioLibrary
    {
        /// <summary>Marks the scenario's hole among the ambient ones.</summary>
        private const float Radius = 61.25f;
        private const float Offset = 400f;

        private readonly ICloudHoles _holes;
        private readonly ICloudSurfaceQuery _surface;
        private readonly CloudBoundaryConfig _boundary;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly ILocalViewCamera _view;

        public CloudSeaScenarioLibrary(ICloudHoles holes, ICloudSurfaceQuery surface, CloudBoundaryConfig boundary,
            IActorRegistry actors, ITimeService time, ILocalViewCamera view)
        {
            _holes = holes;
            _surface = surface;
            _boundary = boundary;
            _actors = actors;
            _time = time;
            _view = view;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("OpenScenarioHole", _ => Open()),
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("ScenarioHoleOpen", _ => CheckOpen()),
        };

        private ScenarioCheck Open()
        {
            IAirshipView? ship = _actors.GetActors<IAirshipView>().FirstOrDefault(airship => airship.IsSimulating && airship.Transform != null);
            if (ship == null) return ScenarioCheck.Fail("no ship to open a hole beside");

            Vector3 at = ship.Transform.position;
            var centre = new Vector2(at.x + Offset, at.z);
            return _holes.TryOpen(new CloudHole(centre, Radius, _time.Time, openSeconds: 1f))
                ? ScenarioCheck.Pass($"hole at {centre}")
                : ScenarioCheck.Fail($"refused ({_holes.Holes.Count} holes open)");
        }

        private ScenarioCheck CheckOpen()
        {
            IReadOnlyList<CloudHoleShape> shapes = _holes.ShapesNow;
            int index = -1;
            for (int i = 0; i < _holes.Holes.Count && i < shapes.Count; i++)
            {
                if (Mathf.Approximately(_holes.Holes[i].Radius, Radius)) index = i;
            }

            if (index < 0) return ScenarioCheck.Fail($"no scenario hole among {_holes.Holes.Count}");

            CloudHoleShape hole = shapes[index];
            if (hole.Openness < 0.999f) return ScenarioCheck.Fail($"openness {hole.Openness:0.000}");

            // The hole sinks the rolling surface by its depth: the swell at that point still moves it (up to its amplitude).
            float height = _surface.GetSurfaceHeight(hole.Centre.x, hole.Centre.y);
            float expected = _boundary.BaseAltitude + CloudSeaFormula.Swell(hole.Centre.x, hole.Centre.y, _time.Time, _boundary.Swell)
                - _boundary.HoleDepth;
            if (Mathf.Abs(height - expected) > 1f) return ScenarioCheck.Fail($"surface at the centre {height:0.0}, expected {expected:0.0}");

            if (_view.Camera != null && Shader.GetGlobalFloat("_TinCanCloudHoleCount") < 1f)
                return ScenarioCheck.Fail("the sea shader has no holes");

            return ScenarioCheck.Pass($"open at {hole.Centre}, surface {height:0.0}, {_holes.Holes.Count} holes");
        }
    }
}
