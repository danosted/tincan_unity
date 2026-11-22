#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.DevTools.Scenarios;
using UnityEditor;

namespace TinCan.DevTools.Tests
{
    /// <summary>
    /// Every scenario runs in a scene that exists and is in the build list; clients load the host's scene through NGO
    /// scene management, so a test scene missing from the list leaves the client out of a host + client run.
    /// </summary>
    public class ScenarioSceneTests
    {
        [Test]
        public void EveryScenarioScene_Exists_AndIsInTheBuildList()
        {
            var buildList = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToHashSet();

            foreach (var entry in ScenarioCatalog.Entries)
            {
                string? path = entry.Scenario.ScenePath;
                if (path == null) continue;

                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, $"{entry.Scenario.Name}: scene '{path}' does not exist. Run TinCan > Dev > Test Range > Rebuild Scenes.");
                Assert.That(buildList, Does.Contain(path), $"{entry.Scenario.Name}: scene '{path}' is not in the build list. Run TinCan > Dev > Test Range > Rebuild Scenes.");
            }
        }

        /// <summary>
        /// The rule (AGENTS.md; .docs/NETWORK_TEST_HARNESS.md, "Running"): a scenario runs in a test-range scene, never in
        /// a game scene, whose state changes with the game, and never in whatever scene happens to be open.
        /// </summary>
        [Test]
        public void EveryScenario_RunsInATestRangeScene()
        {
            var outside = ScenarioCatalog.Entries
                .Where(entry => entry.Scenario.ScenePath == null || !TestScenes.All.Contains(entry.Scenario.ScenePath))
                .Select(entry => $"{entry.Scenario.Name} ({entry.Scenario.ScenePath ?? "the open scene"})");

            Assert.That(outside, Is.Empty, "Scenarios run only in the test-range scenes (TestScenes).");
        }

        [Test]
        public void EveryTestScene_IsUsedByAScenario()
        {
            var used = ScenarioCatalog.Entries.Select(entry => entry.Scenario.ScenePath).ToHashSet();

            Assert.That(TestScenes.All.Where(scene => !used.Contains(scene)), Is.Empty, "A test scene no scenario runs in; remove it or add a scenario.");
        }
    }
}
