#nullable enable
using System.Globalization;
using TinCan.Features.Storm;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario probes for the storm in the world (plan: storm-crossing.md, ST2), on any peer with a view: the storm's
    /// weight on the light and air there (depth into the zone times strength), and whether the shaders have its region.
    /// </summary>
    public sealed class StormScenarioLibrary : IScenarioLibrary
    {
        private readonly StormWeatherPresenter _weather;
        private readonly IStormIntensity _intensity;

        public StormScenarioLibrary(StormWeatherPresenter weather, IStormIntensity intensity)
        {
            _weather = weather;
            _intensity = intensity;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => System.Array.Empty<ScenarioCommand>();

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("StormWeightAbove", at => Compare(at, above: true)),
            new ScenarioProbe("StormWeightBelow", at => Compare(at, above: false)),
            new ScenarioProbe("StormRegionSet", _ => _intensity.Region is { } region
                ? ScenarioCheck.Pass($"the storm stands at {region.Centre}, {region.Radius:0} m round")
                : ScenarioCheck.Fail("no storm region"))
        };

        private ScenarioCheck Compare(string threshold, bool above)
        {
            float limit = float.Parse(threshold, CultureInfo.InvariantCulture);
            string detail = $"storm weight {_weather.Weight:0.00} (depth {_intensity.Depth:0.00}, strength {_intensity.Strength:0.00})";
            bool ok = above ? _weather.Weight > limit : _weather.Weight < limit;
            return ok ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }
    }
}
