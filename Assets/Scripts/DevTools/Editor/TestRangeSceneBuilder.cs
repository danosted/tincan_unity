#nullable enable
using System.Linq;
using TinCan.DevTools.Scenarios;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Test Range > Rebuild Scenes: generates every test-range scene from <see cref="Areas"/>. The scenes
    /// are build output: the same root objects in each (the GameLifetimeScope and NetworkService prefabs, which NGO and
    /// the scope need at the root, a light, a fall catcher, the cloud view with its visuals off), and only the scope's
    /// feature profile and airship differ. Change the set-up here and rebuild rather than editing a scene by hand, so the
    /// scenes cannot drift apart. New scenes are added to the build list, which clients need to load the host's scene.
    /// </summary>
    public static class TestRangeSceneBuilder
    {
        private const string ProfileFolder = "Assets/Settings/FeatureProfiles/Test/";
        private const string ScopePrefab = "Assets/Prefabs/Singletons/GameLifetimeScope.prefab";
        private const string NetworkPrefab = "Assets/Prefabs/Singletons/NetworkService.prefab";
        private const string TestShipPrefab = "Assets/Prefabs/Test/TestShip_Prefab.prefab";
        private const string GroundMaterial = "Assets/Prefabs/Test/TestRange_Rail.mat";

        /// <summary>Areas whose ship is not the test ship: designed ships are built on a bare ship root.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> ShipPrefabs = new()
        {
            [TestScenes.Shipyard] = ShipDesignsAssetBuilder.ModularShipPath,
        };

        /// <summary>Each test-range scene and the feature profile its scope loads.</summary>
        public static readonly (string Scene, string Profile)[] Areas =
        {
            (TestScenes.Core, "Profile_Test_Core"),
            (TestScenes.ShipDamage, "Profile_Test_ShipDamage"),
            (TestScenes.NetCatch, "Profile_Test_NetCatch"),
            (TestScenes.Cannon, "Profile_Test_Cannon"),
            (TestScenes.Voyage, "Profile_Test_Voyage"),
            (TestScenes.Helm, "Profile_Test_Helm"),
            (TestScenes.Shipyard, "Profile_Test_Shipyard")
        };

        [MenuItem("TinCan/Dev/Test Range/Rebuild Scenes")]
        public static void RebuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[TestRange] Stop Play mode before rebuilding the test scenes.");
                return;
            }

            var active = SceneManager.GetActiveScene();
            if (active.isDirty)
            {
                Debug.LogError($"[TestRange] '{active.name}' has unsaved changes; save it before rebuilding the test scenes.");
                return;
            }

            string returnTo = active.path;
            foreach (var (scene, profile) in Areas) Build(scene, profile);
            AddToBuildList();
            if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
            Debug.Log($"[TestRange] Rebuilt {Areas.Length} scene(s): {string.Join(", ", Areas.Select(area => area.Scene))}.");
        }

        /// <summary>Builds one area scene only (the others stay untouched), for a new area.</summary>
        public static void BuildArea(string scenePath)
        {
            var area = Areas.Single(a => a.Scene == scenePath);
            string returnTo = SceneManager.GetActiveScene().path;
            Build(area.Scene, area.Profile);
            AddToBuildList();
            if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        }

        private static void Build(string scenePath, string profileName)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load assets only after NewScene: opening a scene unloads unused assets, so an earlier reference is dead.
            var profile = Load<Object>(ProfileFolder + profileName + ".asset");

            var scope = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ScopePrefab), scene);
            var scopeComponent = scope.GetComponent("ProjectLifetimeScope");
            var fields = new SerializedObject(scopeComponent);
            fields.FindProperty("_airshipPrefab").objectReferenceValue =
                Load<GameObject>(ShipPrefabs.TryGetValue(scenePath, out var ship) ? ship : TestShipPrefab);
            fields.FindProperty("_featureProfile").objectReferenceValue = profile;
            fields.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.InstantiatePrefab(Load<GameObject>(NetworkPrefab), scene);

            var light = new GameObject("Sun", typeof(Light));
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var sun = light.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;

            // The ships spawn about 40 m up; a player who walks off lands here instead of falling forever.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "FallCatcher";
            ground.transform.localScale = new Vector3(50f, 1f, 50f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = Load<Material>(GroundMaterial);

            // The scope resolves the cloud view from the scene; its legacy visuals stay off here.
            var clouds = new GameObject("Cloud Environment (visuals off)");
            var view = clouds.AddComponent<Features.CloudBoundary.CloudEnvironmentView>();
            var viewFields = new SerializedObject(view);
            viewFields.FindProperty("_legacyVisualsEnabled").boolValue = false;
            viewFields.ApplyModifiedPropertiesWithoutUndo();

            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void AddToBuildList()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var (scenePath, _) in Areas)
            {
                if (scenes.Any(entry => entry.path == scenePath)) continue;
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new System.InvalidOperationException($"[TestRange] Missing asset: {path}");
    }
}
