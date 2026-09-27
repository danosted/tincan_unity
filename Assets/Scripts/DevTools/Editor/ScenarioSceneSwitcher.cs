#nullable enable
using TinCan.DevTools.Scenarios;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Opens a scenario's test-range scene before a run, and reopens the scene the developer had open once Play ends.
    /// It never raises a save dialog: with unsaved changes in the open scene the run is refused, so nothing is lost and
    /// automation cannot block on a modal. verify.ps1 opens the scene itself first, in which case nothing is restored
    /// here (the script restores its own starting scene).
    /// </summary>
    [InitializeOnLoad]
    public static class ScenarioSceneSwitcher
    {
        private const string ReturnKey = "TinCan.Scenario.ReturnScene";

        private static bool IsClone => Application.dataPath.Replace('\\', '/').Contains("/Library/VP/");

        static ScenarioSceneSwitcher()
        {
            if (IsClone) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Makes the scenario's scene the open one. False (with an error logged) when it cannot.</summary>
        public static bool OpenFor(string scenarioName)
        {
            if (!ScenarioCatalog.TryGet(scenarioName, out var entry))
            {
                Debug.LogError($"[Scenario] Unknown scenario '{scenarioName}'. Known: {ScenarioCatalog.Names}.");
                return false;
            }

            string? path = entry.Scenario.ScenePath;
            var active = SceneManager.GetActiveScene();
            if (path == null || active.path == path) return true;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogError($"[Scenario] {scenarioName} runs in '{path}', which does not exist.");
                return false;
            }

            if (active.isDirty)
            {
                Debug.LogError($"[Scenario] '{active.name}' has unsaved changes. Save it, then run {scenarioName} again (it opens {path}).");
                return false;
            }

            if (!string.IsNullOrEmpty(active.path) && !SessionState.GetString(ReturnKey, string.Empty).EndsWith(".unity"))
                SessionState.SetString(ReturnKey, active.path);

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log($"[Scenario] Opened {path} for {scenarioName}; the previous scene reopens when Play ends.");
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;

            string path = SessionState.GetString(ReturnKey, string.Empty);
            if (string.IsNullOrEmpty(path)) return;

            SessionState.EraseString(ReturnKey);
            if (SceneManager.GetActiveScene().path == path || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return;

            // After Play the test scene can be flagged modified with nothing worth keeping; never prompt.
            EditorApplication.delayCall += () => EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}
