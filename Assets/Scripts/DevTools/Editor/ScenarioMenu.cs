#nullable enable
using System;
using UnityEditor;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// One menu pair per scenario: <b>(Host)</b> runs every phase on the host against its own player (fast, T2);
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

        [MenuItem(Root + "AimPitch (Host)")]
        public static void AimPitchSolo() => RunSolo("AimPitch");

        [MenuItem(Root + "AimPitch (Host + Client, Lag100)")]
        public static void AimPitchDuo() => RunDuo("AimPitch", "Lag100");

        public static void RunSolo(string scenario) =>
            NetHarnessPlayerTagsMenu.Run(new[] { "autohost", $"scenario:{scenario}", "scenariomode:solo" }, Array.Empty<string>());

        public static void RunDuo(string scenario, string? preset)
        {
            string[] netsim = preset == null ? Array.Empty<string>() : new[] { $"netsim:{preset}" };
            NetHarnessPlayerTagsMenu.Run(
                Concat(new[] { "autohost", $"scenario:{scenario}" }, netsim),
                Concat(new[] { "autojoin", $"scenario:{scenario}" }, netsim));
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
