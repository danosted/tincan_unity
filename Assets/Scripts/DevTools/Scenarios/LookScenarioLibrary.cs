#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Features.Atmosphere;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the look's atmosphere (.docs/plans/look-slice.md, L2). Probes: LookApplied: on this peer the
    /// Atmosphere owns the air and light: the banded air is on for the TinCan shaders, the plain fog and the
    /// three-colour ambient are the look's, the scene's sun has the look's colour and the intensity the frame asks for,
    /// and the local camera renders post effects; the look's shading and surface style are in the shader globals and
    /// keywords (.docs/plans/artstyle-pipeline-breakdown.md). Presentation, so every peer checks its own.
    /// </summary>
    public sealed class LookScenarioLibrary : IScenarioLibrary
    {
        private const float Tolerance = 0.01f;

        private readonly AtmospherePresenter _atmosphere;
        private readonly LookProfile _look;
        private readonly ISurfaceStyle _surface;
        private readonly ILocalViewCamera _view;

        public LookScenarioLibrary(AtmospherePresenter atmosphere, LookProfile look, ISurfaceStyle surface, ILocalViewCamera view)
        {
            _atmosphere = atmosphere;
            _look = look;
            _surface = surface;
            _view = view;
        }

        public IEnumerable<ScenarioCommand> Commands => System.Array.Empty<ScenarioCommand>();

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("LookApplied", _ => CheckApplied()),
        };

        private ScenarioCheck CheckApplied()
        {
            var problems = new List<string>();
            var frame = _atmosphere.Current;
            if (Shader.GetGlobalFloat("_TinCanAtmosphereOn") < 0.5f) problems.Add("the banded air is off");
            if (!RenderSettings.fog) problems.Add("the plain fog is off");
            if (!Near(RenderSettings.fogColor, Color.Lerp(frame.AwayFog, frame.SunwardFog, 0.35f))) problems.Add($"fog colour {RenderSettings.fogColor}");
            if (RenderSettings.ambientMode != AmbientMode.Trilight) problems.Add($"ambient mode {RenderSettings.ambientMode}");
            if (!Near(RenderSettings.ambientSkyColor, frame.AmbientSky)) problems.Add($"ambient sky {RenderSettings.ambientSkyColor}");

            if (!LookShaderGlobals.IsEnabled) problems.Add("the look's shading is off");
            else if ((LookShaderGlobals.CurrentShading - _look.Shading.ToVector()).magnitude > Tolerance) problems.Add($"shading {LookShaderGlobals.CurrentShading}, look {_look.Shading.ToVector()}");
            var keyword = _surface.Evaluate().Keyword;
            foreach (var styleKeyword in LookShaderGlobals.SurfaceKeywords)
            {
                if (Shader.IsKeywordEnabled(styleKeyword) != (styleKeyword == keyword)) problems.Add($"surface keyword {styleKeyword} is {(Shader.IsKeywordEnabled(styleKeyword) ? "on" : "off")}");
            }

            var sun = RenderSettings.sun;
            if (sun == null) problems.Add("the scene names no sun");
            else
            {
                if (!Near(sun.color, _look.Settings.SunColour)) problems.Add($"sun colour {sun.color}, look {_look.Settings.SunColour}");
                if (Mathf.Abs(sun.intensity - frame.SunIntensity) > Tolerance) problems.Add($"sun intensity {sun.intensity:0.###}, frame {frame.SunIntensity:0.###}");
            }

            var camera = _view.Camera;
            if (camera == null) problems.Add("no local camera yet");
            else if (!camera.GetUniversalAdditionalCameraData().renderPostProcessing) problems.Add("post-processing is off on the local camera");

            return problems.Count == 0
                ? ScenarioCheck.Pass($"air on, fog {RenderSettings.fogDensity:0.#####}/m, sun {frame.SunIntensity:0.##}, post on")
                : ScenarioCheck.Fail(string.Join("; ", problems));
        }

        private static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) <= Tolerance && Mathf.Abs(a.g - b.g) <= Tolerance && Mathf.Abs(a.b - b.b) <= Tolerance;
    }
}
