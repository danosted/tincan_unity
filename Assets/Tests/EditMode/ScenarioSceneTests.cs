#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.DevTools.Scenarios;
using UnityEditor;

namespace TinCan.Tests.EditMode
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

        [Test]
        public void EveryTestScene_IsUsedByAScenario()
        {
            var used = ScenarioCatalog.Entries.Select(entry => entry.Scenario.ScenePath).ToHashSet();

            Assert.That(TestScenes.All.Where(scene => !used.Contains(scene)), Is.Empty, "A test scene no scenario runs in; remove it or add a scenario.");
        }
    }
}
