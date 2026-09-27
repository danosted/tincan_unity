#nullable enable
using UnityEditor;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Targeting > Draw All Queries: every targeting query (interaction prompts, repair scans on the server
    /// and the owner's predicted ones, scenario probes) draws its shape and result, green on a hit and red on a miss.
    /// Visible in the Scene view, and in the Game view with Gizmos on. The switch is a flag file in the main project's
    /// Library (TargetingDebug.FlagPath), so the host and MPPM virtual players share it; EditorPrefs are not shared.
    /// The interaction prompt always draws its own query regardless.
    /// </summary>
    public static class TargetingDebugMenu
    {
        private const string MenuPath = "TinCan/Dev/Targeting/Draw All Queries";

        [MenuItem(MenuPath)]
        private static void Toggle() =>
            TinCan.Features.Targeting.TargetingDebug.SetDrawAllQueries(!System.IO.File.Exists(TinCan.Features.Targeting.TargetingDebug.FlagPath));

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, System.IO.File.Exists(TinCan.Features.Targeting.TargetingDebug.FlagPath));
            return true;
        }
    }
}
