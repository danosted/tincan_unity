#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Atmosphere;
using TinCan.Features.Atmosphere;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the moving day (.docs/plans/day-cycle.md, D2). Presentation, so every peer checks its own.
    /// Probes: DayApplied: the scene's sun shines from where the day's <see cref="ISunSource"/> says, at the colour the
    /// atmosphere's frame gives it, the painted sky is the skybox and its day globals are on. SunMoved: the sun has
    /// turned since MarkSun. Command: MarkSun (remembers where the sun is on this peer).
    /// </summary>
    public sealed class DayCycleScenarioLibrary : IScenarioLibrary
    {
        private const float Tolerance = 0.01f;
        /// <summary>Degrees the sun must turn after MarkSun to count as moving.</summary>
        private const float MinTurn = 0.2f;

        private readonly ISunSource _sun;
        private readonly AtmospherePresenter _atmosphere;
        private readonly LookProfile _look;
        private Vector3? _marked;

        public DayCycleScenarioLibrary(ISunSource sun, AtmospherePresenter atmosphere, LookProfile look)
        {
            _sun = sun;
            _atmosphere = atmosphere;
            _look = look;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("MarkSun", _ => Mark()),
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("DayApplied", _ => CheckApplied()),
            new ScenarioProbe("SunMoved", _ => CheckMoved()),
        };

        private ScenarioCheck Mark()
        {
            _marked = _sun.ToSun;
            return ScenarioCheck.Pass($"sun at {_sun.Hour:0.00}h, {_sun.Elevation:0.0} deg");
        }

        private ScenarioCheck CheckApplied()
        {
            var problems = new List<string>();
            var light = RenderSettings.sun;
            if (light == null) problems.Add("the scene names no sun");
            else
            {
                float off = Vector3.Angle(light.transform.forward, -_sun.ToSun);
                if (off > 0.5f) problems.Add($"the sun is {off:0.0} deg off the day's");
                var colour = _atmosphere.Current.SunColour;
                if (Mathf.Abs(light.color.r - colour.r) > Tolerance || Mathf.Abs(light.color.g - colour.g) > Tolerance
                    || Mathf.Abs(light.color.b - colour.b) > Tolerance) problems.Add($"sun colour {light.color}, frame {colour}");
            }

            if (_look.Day.Length == 0) problems.Add("the look has no day keys");
            if (Shader.GetGlobalFloat("_TinCanSkyOn") < 0.5f) problems.Add("the sky's day globals are off");
            if (_look.Sky != null && RenderSettings.skybox != _look.Sky) problems.Add($"skybox {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}, look {_look.Sky.name}");

            return problems.Count == 0
                ? ScenarioCheck.Pass($"sun {_sun.Hour:0.00}h at {_sun.Elevation:0.0} deg")
                : ScenarioCheck.Fail(string.Join("; ", problems));
        }

        private ScenarioCheck CheckMoved()
        {
            if (_marked == null) return ScenarioCheck.Fail("MarkSun first");
            float turn = Vector3.Angle(_marked.Value, _sun.ToSun);
            return turn >= MinTurn
                ? ScenarioCheck.Pass($"turned {turn:0.00} deg, now {_sun.Hour:0.00}h")
                : ScenarioCheck.Fail($"turned only {turn:0.000} deg");
        }
    }
}
