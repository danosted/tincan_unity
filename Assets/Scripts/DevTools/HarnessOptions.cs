#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;

namespace TinCan.DevTools
{
    /// <summary>
    /// What the network test harness should do in this instance, read from <see cref="LaunchArguments"/>:
    /// <c>-netsim &lt;preset&gt;</c>, <c>-bot &lt;route&gt;</c>, <c>-telemetry</c>, <c>-scenario &lt;name&gt;</c> and
    /// <c>-scenariomode solo</c>. A bot implies telemetry. With none of them the harness stays inert.
    /// </summary>
    public sealed class HarnessOptions
    {
        public const string NetSimFlag = "-netsim";
        public const string BotFlag = "-bot";
        public const string TelemetryFlag = "-telemetry";
        public const string ScenarioFlag = "-scenario";
        public const string ScenarioModeFlag = "-scenariomode";
        public const string SoloMode = "solo";

        public string? NetworkPreset { get; }
        public string? BotRoute { get; }
        public bool TelemetryEnabled { get; }
        public string? Scenario { get; }
        /// <summary>The host plays every scenario phase against its own player; no client is expected.</summary>
        public bool ScenarioSolo { get; }

        public bool IsActive => NetworkPreset != null || BotRoute != null || TelemetryEnabled || Scenario != null;

        /// <summary>True while scripted play runs, so rules that would disturb it (resets) are suspended.</summary>
        public bool IsScripted => BotRoute != null || Scenario != null;

        public HarnessOptions(string? networkPreset, string? botRoute, bool telemetryEnabled, string? scenario = null, bool scenarioSolo = false)
        {
            NetworkPreset = networkPreset;
            BotRoute = botRoute;
            TelemetryEnabled = telemetryEnabled || botRoute != null;
            Scenario = scenario;
            ScenarioSolo = scenario != null && scenarioSolo;
        }

        public static HarnessOptions Parse(IReadOnlyList<string> args)
        {
            string? preset = LaunchArguments.TryGetValue(args, NetSimFlag, out var presetValue) ? presetValue : null;
            string? route = LaunchArguments.TryGetValue(args, BotFlag, out var routeValue) ? routeValue : null;
            string? scenario = LaunchArguments.TryGetValue(args, ScenarioFlag, out var scenarioValue) ? scenarioValue : null;
            bool solo = LaunchArguments.TryGetValue(args, ScenarioModeFlag, out var mode) &&
                        string.Equals(mode, SoloMode, System.StringComparison.OrdinalIgnoreCase);
            return new HarnessOptions(preset, route, LaunchArguments.HasFlag(args, TelemetryFlag), scenario, solo);
        }
    }
}
