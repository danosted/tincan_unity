#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;

namespace TinCan.DevTools
{
    /// <summary>
    /// What the network test harness should do in this instance, read from <see cref="LaunchArguments"/>:
    /// <c>-netsim &lt;preset&gt;</c>, <c>-bot &lt;route&gt;</c>, <c>-telemetry</c>. A bot implies telemetry.
    /// With none of them the harness stays inert.
    /// </summary>
    public sealed class HarnessOptions
    {
        public const string NetSimFlag = "-netsim";
        public const string BotFlag = "-bot";
        public const string TelemetryFlag = "-telemetry";

        public string? NetworkPreset { get; }
        public string? BotRoute { get; }
        public bool TelemetryEnabled { get; }

        public bool IsActive => NetworkPreset != null || BotRoute != null || TelemetryEnabled;

        public HarnessOptions(string? networkPreset, string? botRoute, bool telemetryEnabled)
        {
            NetworkPreset = networkPreset;
            BotRoute = botRoute;
            TelemetryEnabled = telemetryEnabled || botRoute != null;
        }

        public static HarnessOptions Parse(IReadOnlyList<string> args)
        {
            string? preset = LaunchArguments.TryGetValue(args, NetSimFlag, out var presetValue) ? presetValue : null;
            string? route = LaunchArguments.TryGetValue(args, BotFlag, out var routeValue) ? routeValue : null;
            return new HarnessOptions(preset, route, LaunchArguments.HasFlag(args, TelemetryFlag));
        }
    }
}
