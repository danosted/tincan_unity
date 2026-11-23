#nullable enable
using System;
using System.IO;
using System.Linq;
using TinCan.Core.Audio;
using TinCan.Core.Domain.Features;
using TinCan.Features.Atmosphere;
using TinCan.Features.CloudBoundary.Atmosphere;
using TinCan.Features.Storm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Storm > Build Lab: the storm lab (plan: .docs/plans/storm-crossing.md, ST1).
    /// <list type="bullet">
    /// <item>stand-in sounds (<see cref="StormSynth"/>) in Assets/Audio/Storm/;</item>
    /// <item>StormConfig (its look from the palette: slate and plum, never grey; built only when missing, so tuning in
    /// the Inspector is kept), the lab's sliders (StormLab), the installer, and Profile_Lab_Storm: the game's profile
    /// plus the storm;</item>
    /// <item><see cref="ScenePath"/>: a copy of cheesed_scene on the lab profile, its volumetric clouds on their own
    /// profile copy (the cloud director writes into its profile). Rebuilding copies the game scene again. It is put on the
    /// build list, which Netcode needs to load it (the scene itself is git-ignored build output).</item>
    /// </list>
    /// Press Play in Lab_Storm, start a host, select Assets/Settings/Storm/StormLab and drag Depth and Strength.
    /// </summary>
    public static class StormLabBuilder
    {
        public const string ScenePath = "Assets/Scenes/Lab/Lab_Storm.unity";
        private const string GameScenePath = "Assets/Scenes/cheesed_scene.unity";
        private const string Folder = "Assets/Settings/Storm/";
        private const string ConfigPath = Folder + "StormConfig.asset";
        private const string LabPath = Folder + "StormLab.asset";
        private const string CloudsProfilePath = Folder + "VolumetricClouds_StormLab.asset";
        private const string InstallerPath = "Assets/Resources/Installers/StormFeatureInstaller.asset";
        private const string ProfilePath = "Assets/Settings/FeatureProfiles/Lab/Profile_Lab_Storm.asset";
        private const string AudioFolder = "Assets/Audio/Storm/";
        private const string AudioConfigPath = "Assets/Audio/AudioConfig.asset";

        [MenuItem("TinCan/Dev/Storm/Build Lab")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[StormLab] Stop Play mode before building the storm lab.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            WriteClips();
            var config = Config();
            var lab = Asset<StormLabSettings>(LabPath, _ => { });
            var installer = Asset<StormFeatureInstaller>(InstallerPath, i =>
            {
                var fields = new SerializedObject(i);
                fields.FindProperty("_config").objectReferenceValue = config;
                fields.FindProperty("_lab").objectReferenceValue = lab;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
            var profile = Asset<FeatureProfile>(ProfilePath, p =>
            {
                var fields = new SerializedObject(p);
                SetList(fields.FindProperty("_includes"), Load<Object>(ShipSocketsAssetBuilder.GalleonProfilePath));
                SetList(fields.FindProperty("_installers"), installer);
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
            AssetDatabase.SaveAssets();

            string returnTo = SceneManager.GetActiveScene().path;
            BuildScene();
            if (!string.IsNullOrEmpty(returnTo) && returnTo != ScenePath) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
            Debug.Log($"[StormLab] Built {ScenePath}: press Play, start a host, and drag Depth and Strength on {LabPath}.");
        }

        private static void WriteClips()
        {
            Directory.CreateDirectory(AudioFolder);
            File.WriteAllBytes(AudioFolder + "storm_wind_loop.wav", AudioLabSynth.Wav(StormSynth.Wind()));
            File.WriteAllBytes(AudioFolder + "storm_rain_loop.wav", AudioLabSynth.Wav(StormSynth.Rain()));
            File.WriteAllBytes(AudioFolder + "storm_heart_loop.wav", AudioLabSynth.Wav(StormSynth.Heart()));
            for (int i = 0; i < 3; i++) File.WriteAllBytes($"{AudioFolder}storm_thunder_{i}.wav", AudioLabSynth.Wav(StormSynth.Thunder(31 + i * 17)));
            AssetDatabase.Refresh();
        }

        /// <summary>The storm's config: made with its look from the palette when missing; its clips and output always.</summary>
        private static StormConfig Config()
        {
            bool made = AssetDatabase.LoadAssetAtPath<StormConfig>(ConfigPath) == null;
            return Asset<StormConfig>(ConfigPath, config =>
            {
                if (made) config.Look = PaletteLook();
                // Fields added after the config was first made get their first guess too (the rest of the tuning is kept).
                if (config.Look.CloudShade == default) config.Look.CloudShade = PaletteLook().CloudShade;
                if (config.Clouds.Density <= 0f) config.Clouds = StormClouds();
                config.Output = Load<AudioConfig>(AudioConfigPath).Ambience;
                config.WindLoop = Load<AudioClip>(AudioFolder + "storm_wind_loop.wav");
                config.RainLoop = Load<AudioClip>(AudioFolder + "storm_rain_loop.wav");
                config.HeartLoop = Load<AudioClip>(AudioFolder + "storm_heart_loop.wav");
                config.Thunder = Enumerable.Range(0, 3).Select(i => Load<AudioClip>($"{AudioFolder}storm_thunder_{i}.wav")).ToArray();
            });
        }

        /// <summary>
        /// The heart of a raging storm from the palette (ART_DIRECTION pillar 3: shadows are coloured, never grey): a
        /// dim, cooled sun, slate and plum ambient, thick slate fog, and a pale spirit-cyan flash. A first guess, tuned by eye.
        /// </summary>
        private static WeatherLook PaletteLook()
        {
            var palette = LookPalette.Load();
            Color slate = palette["shadow-tint"], plum = palette["plum"], mulberry = palette["mulberry"], mist = palette["mist"];
            Color cloudShadow = palette["cloud-shadow"], cyan = palette["spirit-cyan"];
            return new WeatherLook
            {
                SunScale = 0.25f,
                SunColour = mist,
                AmbientSky = slate * 0.55f,
                AmbientHorizon = Color.Lerp(slate, mulberry, 0.35f) * 0.5f,
                AmbientGround = plum * 0.7f,
                SunwardFog = cloudShadow * 0.6f,
                AwayFog = Color.Lerp(slate, plum, 0.3f) * 0.55f,
                FogDensity = 0.006f,
                HazeDensity = 0.0025f,
                MaxFog = 0.94f,
                CloudShade = Color.Lerp(slate, plum, 0.25f) * 0.75f,
                FlashColour = Color.Lerp(cyan, Color.white, 0.5f) * 0.9f
            };
        }

        /// <summary>
        /// The overcast at the heart, against the game's clouds (density 0.15, shape 0.6, ambient 3, variation 0.6 in
        /// cheesed_scene): denser, more of the sky covered, even, with darker cores. A first guess, tuned by eye.
        /// </summary>
        private static CloudWeatherTarget StormClouds() => new()
        {
            Density = 0.45f,
            ShapeFactor = 0.35f,
            OpacityVariation = 0.1f,
            AmbientDimmer = 1f
        };

        private static void BuildScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
            EnsureFolder(ScenePath);
            if (!AssetDatabase.CopyAsset(GameScenePath, ScenePath)) throw new InvalidOperationException($"could not copy {GameScenePath} to {ScenePath}");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            var scope = roots.Select(o => o.GetComponent("ProjectLifetimeScope")).FirstOrDefault(c => c != null)
                        ?? throw new InvalidOperationException($"{ScenePath} has no ProjectLifetimeScope at its root.");
            var scopeFields = new SerializedObject(scope);
            // Loaded again here: opening the scene unloaded the assets loaded before it, so the caller's reference is dead.
            scopeFields.FindProperty("_featureProfile").objectReferenceValue = Load<FeatureProfile>(ProfilePath);
            scopeFields.ApplyModifiedPropertiesWithoutUndo();

            // The volumetric clouds on the lab's own profile copy: the director writes into its profile asset.
            var director = roots.SelectMany(o => o.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(c => c != null && c.GetType().Name == "CloudAtmosphereDirector");
            var directorFields = director != null ? new SerializedObject(director) : null;
            var gameClouds = directorFields?.FindProperty("_cloudsProfile").objectReferenceValue as VolumeProfile;
            if (gameClouds != null)
            {
                if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(CloudsProfilePath) == null)
                    AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(gameClouds), CloudsProfilePath);
                var labClouds = Load<VolumeProfile>(CloudsProfilePath);
                directorFields!.FindProperty("_cloudsProfile").objectReferenceValue = labClouds;
                directorFields.ApplyModifiedPropertiesWithoutUndo();
                foreach (var volume in roots.SelectMany(o => o.GetComponentsInChildren<Volume>(true)))
                {
                    if (volume.sharedProfile == gameClouds) volume.sharedProfile = labClouds;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Netcode loads only scenes on the build list (else it stops Play with a dialog).
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(entry => entry.path != ScenePath)) EditorBuildSettings.scenes = scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
        }

        private static void SetList(SerializedProperty list, params Object[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static T Asset<T>(string path, Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                EnsureFolder(path);
                AssetDatabase.CreateAsset(asset, path);
            }

            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Missing asset {path}.");

        private static void EnsureFolder(string assetPath) => EnsureDirectory(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));

        private static void EnsureDirectory(string? folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
