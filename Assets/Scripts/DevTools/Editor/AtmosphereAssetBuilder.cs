#nullable enable
using System;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Features.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Look > Build Atmosphere: the golden-afternoon look (<see cref="LookProfile"/>) with its colours
    /// from the palette (<see cref="LookPalette"/>; numbers are set when the asset is made and then left as tuned), its
    /// post volume (the wide, soft bloom that gives glows a halo), the Atmosphere installer, and that installer in the
    /// play scenes' profiles (Profile_Galleon, Profile_FuelSandbox), and the one test area with the look (Profile_Test_Look:
    /// Test_Core plus Atmosphere, for the LookApplies scenario). The other test areas stay plain. Plan:
    /// .docs/plans/look-slice.md (L2).
    /// </summary>
    public static class AtmosphereAssetBuilder
    {
        public const string LookPath = "Assets/Settings/Look/Look_GoldenAfternoon.asset";
        public const string PostPath = "Assets/Settings/Look/Look_GoldenAfternoon_Post.asset";
        public const string InstallerPath = "Assets/Resources/Installers/AtmosphereFeatureInstaller.asset";
        public const string TestProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Look.asset";
        private const string TestCoreProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Core.asset";
        public static readonly string[] PlayProfiles =
        {
            "Assets/Settings/FeatureProfiles/Profile_Galleon.asset",
            "Assets/Settings/FeatureProfiles/Profile_FuelSandbox.asset",
        };

        [MenuItem("TinCan/Dev/Look/Build Atmosphere")]
        public static void Build()
        {
            var palette = LookPalette.Load();
            var post = Post();
            var look = Asset<LookProfile>(LookPath, profile =>
            {
                var settings = profile.Settings;
                settings.SunColour = Color.Lerp(Color.white, palette["leaf-gold"], 0.3f);
                settings.AmbientSky = palette["sky"] * 0.85f;
                settings.AmbientHorizon = palette["horizon-haze"] * 0.85f;
                settings.AmbientGround = palette["cloud-shadow"] * 0.85f;
                settings.SunwardFog = Color.Lerp(palette["horizon-haze"], palette["leaf-gold"], 0.35f);
                settings.AwayFog = Color.Lerp(palette["mist"], palette["cloud-shadow"], 0.5f);
                settings.WhiteoutColour = palette["cloud"];
                profile.Settings = settings;
                profile.Post = post;
            });

            var installer = Asset<AtmosphereFeatureInstaller>(InstallerPath, i => SetField(i, "_look", look));
            foreach (var path in PlayProfiles) AddUnique(AssetDatabase.LoadAssetAtPath<FeatureProfile>(path), "_installers", installer);
            Asset<FeatureProfile>(TestProfilePath, profile =>
            {
                AddUnique(profile, "_includes", AssetDatabase.LoadAssetAtPath<FeatureProfile>(TestCoreProfilePath));
                AddUnique(profile, "_installers", installer);
            });

            AssetDatabase.SaveAssets();
            Debug.Log($"[AtmosphereAssetBuilder] {look.name} built; {installer.name} in {string.Join(", ", PlayProfiles)}");
        }

        /// <summary>The look's post volume: bloom with a high threshold and a wide scatter, so only glows bloom, softly.</summary>
        private static VolumeProfile Post()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                EnsureDirectory(Path.GetDirectoryName(PostPath));
                AssetDatabase.CreateAsset(profile, PostPath);
            }

            if (!profile.TryGet<Bloom>(out var bloom))
            {
                bloom = profile.Add<Bloom>(overrides: true);
                bloom.name = nameof(Bloom);
                AssetDatabase.AddObjectToAsset(bloom, profile);
                bloom.threshold.value = 1.1f;
                bloom.intensity.value = 0.7f;
                bloom.scatter.value = 0.85f;
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Asset<T>(string path, Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                EnsureDirectory(Path.GetDirectoryName(path));
                AssetDatabase.CreateAsset(asset, path);
            }

            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void SetField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddUnique(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
            }

            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureDirectory(string? folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            folder = folder!.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            EnsureDirectory(Path.GetDirectoryName(folder));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)!.Replace('\\', '/'), Path.GetFileName(folder));
        }
    }
}
