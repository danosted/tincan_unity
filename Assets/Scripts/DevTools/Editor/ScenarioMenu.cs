#nullable enable
using System;
using System.Globalization;
using TinCan.DevTools.Scenarios;
using UnityEditor;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// One menu pair per scenario: each opens the scenario's test-range scene first (ScenarioSceneSwitcher); <b>(Host)</b> runs every phase on the host against its own player (fast, T2);
    /// <b>(Host + Client, Lag100)</b> runs arrange/assert on the host and act on a lagged client (T3). The run ends Play
    /// by itself and leaves <c>Logs/feature-telemetry/&lt;name&gt;/latest-summary.json</c>. Scripted form:
    /// <c>.tools/run-scenario.ps1 -Name &lt;name&gt; [-Mode Solo|Duo]</c>. See .docs/NETWORK_TEST_HARNESS.md.
    /// </summary>
    public static class ScenarioMenu
    {
        private const string Root = "TinCan/Dev/Scenarios/";

        [MenuItem(Root + "NetCatch (Host)")]
        public static void NetCatchSolo() => RunSolo("NetCatch");

        [MenuItem(Root + "NetCatch (Host + Client, Lag100)")]
        public static void NetCatchDuo() => RunDuo("NetCatch", "Lag100");

        [MenuItem(Root + "TagRequest (Host)")]
        public static void TagRequestSolo() => RunSolo("TagRequest");

        [MenuItem(Root + "TagRequest (Host + Client, Lag100)")]
        public static void TagRequestDuo() => RunDuo("TagRequest", "Lag100");

        [MenuItem(Root + "EquipCycle (Host)")]
        public static void EquipCycleSolo() => RunSolo("EquipCycle");

        [MenuItem(Root + "EquipCycle (Host + Client, Lag100)")]
        public static void EquipCycleDuo() => RunDuo("EquipCycle", "Lag100");

        [MenuItem(Root + "ShipDamage (Host)")]
        public static void ShipDamageSolo() => RunSolo("ShipDamage");

        [MenuItem(Root + "ShipDamage (Host + Client, Lag100)")]
        public static void ShipDamageDuo() => RunDuo("ShipDamage", "Lag100");

        [MenuItem(Root + "RepairLoop (Host)")]
        public static void RepairLoopSolo() => RunSolo("RepairLoop");

        [MenuItem(Root + "RepairLoop (Host + Client, Lag100)")]
        public static void RepairLoopDuo() => RunDuo("RepairLoop", "Lag100");

        [MenuItem(Root + "ShipDamageLateJoin (Host)")]
        public static void ShipDamageLateJoinSolo() => RunSolo("ShipDamageLateJoin");

        [MenuItem(Root + "ShipDamageLateJoin (Host + Client, Lag100)")]
        public static void ShipDamageLateJoinDuo() => RunDuo("ShipDamageLateJoin", "Lag100");

        [MenuItem(Root + "AimPitch (Host)")]
        public static void AimPitchSolo() => RunSolo("AimPitch");

        [MenuItem(Root + "AimPitch (Host + Client, Lag100)")]
        public static void AimPitchDuo() => RunDuo("AimPitch", "Lag100");

        [MenuItem(Root + "InteractRack (Host)")]
        public static void InteractRackSolo() => RunSolo("InteractRack");

        [MenuItem(Root + "InteractRack (Host + Client, Lag100)")]
        public static void InteractRackDuo() => RunDuo("InteractRack", "Lag100");

        public static void RunSolo(string scenario)
        {
            if (!ScenarioSceneSwitcher.OpenFor(scenario)) return;
            NetHarnessPlayerTagsMenu.Run(new[] { "autohost", $"scenario:{scenario}", "scenariomode:solo" }, Array.Empty<string>());
        }

        public static void RunDuo(string scenario, string? preset)
        {
            if (!ScenarioSceneSwitcher.OpenFor(scenario)) return;
            string[] netsim = preset == null ? Array.Empty<string>() : new[] { $"netsim:{preset}" };
            // A late-join scenario holds the client back, so the host's arrange steps run before it connects.
            float joinDelay = ScenarioCatalog.TryGet(scenario, out var entry) ? entry.Scenario.ClientJoinDelaySeconds : 0f;
            string[] join = joinDelay > 0f
                ? new[] { "autojoin", $"joindelay:{joinDelay.ToString(CultureInfo.InvariantCulture)}", $"scenario:{scenario}" }
                : new[] { "autojoin", $"scenario:{scenario}" };
            NetHarnessPlayerTagsMenu.Run(
                Concat(new[] { "autohost", $"scenario:{scenario}" }, netsim),
                Concat(join, netsim));
        }

        private static string[] Concat(string[] first, string[] second)
        {
            var result = new string[first.Length + second.Length];
            first.CopyTo(result, 0);
            second.CopyTo(result, first.Length);
            return result;
        }
    }
}
