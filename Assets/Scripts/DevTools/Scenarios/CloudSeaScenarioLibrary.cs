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
    /// WebRiftIsReal: the web of rifts is in the game's surface on this peer (<c>CheckWeb</c>).
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
        private readonly IReadOnlyList<CloudSeaConfig> _sea;

        public CloudSeaScenarioLibrary(ICloudHoles holes, ICloudSurfaceQuery surface, CloudBoundaryConfig boundary,
            IActorRegistry actors, ITimeService time, ILocalViewCamera view, IReadOnlyList<CloudSeaConfig> sea)
        {
            _holes = holes;
            _surface = surface;
            _boundary = boundary;
            _actors = actors;
            _time = time;
            _view = view;
            _sea = sea;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("OpenScenarioHole", _ => Open()),
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("ScenarioHoleOpen", _ => CheckOpen()),
            new ScenarioProbe("WebRiftIsReal", _ => CheckWeb()),
        };

        // The web of rifts (plan .docs/plans/cloud-sea-fog.md, F3) is the game's surface on this peer: near the first
        // ship, a point deep in a rift has the surface down by the hole depth, a point on a cloud island has it at the
        // swell, and where this peer has a view the sea shader has the web.
        private ScenarioCheck CheckWeb()
        {
            if (_sea.Count == 0) return ScenarioCheck.Fail("no Cloud Sea feature");
            CloudWeb web = _sea[0].Web(_boundary.WorldSeed);
            if (!web.IsOn) return ScenarioCheck.Fail("the web is off (Open Share 0)");

            IAirshipView? ship = _actors.GetActors<IAirshipView>().FirstOrDefault(airship => airship.Transform != null);
            if (ship == null) return ScenarioCheck.Fail("no ship");

            Vector3 at = ship.Transform.position;
            float now = _time.Time;
            Vector2? rift = null, cloud = null;
            for (int i = 0; i < 160 * 160 && (rift == null || cloud == null); i++)
            {
                float x = at.x + (i % 160 - 80) * 25f, z = at.z + (i / 160 - 80) * 25f;
                float cut = CloudSeaFormula.WebCut(x, z, now, web);
                if (cut > 0.999f) rift ??= new Vector2(x, z);
                else if (cut < 0.001f && CloudSeaFormula.HoleCut(x, z, _holes.ShapesNow) < 0.001f) cloud ??= new Vector2(x, z);
            }

            if (rift == null) return ScenarioCheck.Fail("no rift within 2 km of the ship");
            if (cloud == null) return ScenarioCheck.Fail("no cloud island within 2 km of the ship");

            float Swell(Vector2 p) => _boundary.BaseAltitude + CloudSeaFormula.Swell(p.x, p.y, now, _boundary.Swell);
            float inRift = _surface.GetSurfaceHeight(rift.Value.x, rift.Value.y);
            float onCloud = _surface.GetSurfaceHeight(cloud.Value.x, cloud.Value.y);
            if (Mathf.Abs(inRift - (Swell(rift.Value) - _boundary.HoleDepth)) > 1f)
                return ScenarioCheck.Fail($"surface in the rift at {rift} is {inRift:0.0}, not {Swell(rift.Value) - _boundary.HoleDepth:0.0}");
            if (Mathf.Abs(onCloud - Swell(cloud.Value)) > 1f)
                return ScenarioCheck.Fail($"surface on the cloud at {cloud} is {onCloud:0.0}, not {Swell(cloud.Value):0.0}");

            if (_view.Camera != null && Shader.GetGlobalVector("_TinCanCloudWebMotion").x <= 0f)
                return ScenarioCheck.Fail("the sea shader has no web");

            return ScenarioCheck.Pass($"rift at {rift} ({inRift:0}), cloud at {cloud} ({onCloud:0})");
        }

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
