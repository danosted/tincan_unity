#nullable enable
using System;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Settings;
using TinCan.Features.CloudMist;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Cloud Mist > Build Assets (plan .docs/plans/cloud-sea-fog.md, F4): the mist's quality tiers
    /// (<see cref="CloudMistQuality"/>), its look (the material), the graphics.clouds setting, the installer in every
    /// profile that has the Cloud Sea feature, and <see cref="CloudMistRendererFeature"/> on _MainURPRenderer. Run it
    /// again safely: existing assets keep their tuning, and nothing is added twice.
    /// </summary>
    public static class CloudMistAssetBuilder
    {
        private const string QualityPath = "Assets/Settings/CloudMist/CloudMistQuality.asset";
        private const string MaterialPath = "Assets/Materials/Clouds/M_CloudSeaMist.mat";
        private const string ShaderName = "TinCan/Environment/Cloud Sea Mist";
        private const string SettingPath = "Assets/Settings/PlayerSettings/Setting_Clouds.asset";
        private const string InstallerPath = "Assets/Resources/Installers/CloudMistFeatureInstaller.asset";
        private const string RendererPath = "Assets/Settings/_MainURPRenderer.asset";
        private static readonly string[] Profiles =
        {
            "Assets/Settings/FeatureProfiles/Profile_Galleon.asset",
            "Assets/Settings/FeatureProfiles/Profile_FuelSandbox.asset",
            "Assets/Settings/FeatureProfiles/Test/Profile_Test_Look.asset",
        };

        [MenuItem("TinCan/Dev/Cloud Mist/Build Assets")]
        public static void Build()
        {
            var quality = Asset<CloudMistQuality>(QualityPath);
            var material = Material();
            var setting = Setting();
            var installer = Asset<CloudMistFeatureInstaller>(InstallerPath);
            SetField(installer, "_quality", quality);
            SetField(installer, "_setting", setting);
            foreach (string path in Profiles)
            {
                var profile = AssetDatabase.LoadAssetAtPath<FeatureProfile>(path) ?? throw new InvalidOperationException($"Missing asset {path}.");
                AddUnique(profile, "_installers", installer);
            }

            RendererFeature(quality, material);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CloudMistAssetBuilder] {installer.name} in {string.Join(", ", Profiles)}; the mist's renderer feature is on {RendererPath}.");
        }

        private static Material Material()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            var shader = Shader.Find(ShaderName) ?? throw new InvalidOperationException($"Shader {ShaderName} is missing (compile errors?).");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(MaterialPath) };
            EnsureDirectory(Path.GetDirectoryName(MaterialPath));
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static SettingDefinition Setting()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SettingDefinition>(SettingPath);
            if (existing != null) return existing;

            var built = SettingDefinition.Choice(CloudMistQuality.SettingKey, "Clouds", SettingGroup.Graphics, CloudMistTierProcessor.Auto,
                CloudMistTierProcessor.Auto, CloudMistTierProcessor.Off, "Low", "Medium", "High", "Ultra");
            built.name = Path.GetFileNameWithoutExtension(SettingPath);
            EnsureDirectory(Path.GetDirectoryName(SettingPath));
            AssetDatabase.CreateAsset(built, SettingPath);
            return built;
        }

        // Adds the feature as a sub-asset of the renderer, the way the renderer's Inspector does, unless it is there.
        private static void RendererFeature(CloudMistQuality quality, Material material)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath) ?? throw new InvalidOperationException($"Missing asset {RendererPath}.");
            CloudMistRendererFeature? feature = null;
            foreach (var existing in renderer.rendererFeatures)
            {
                if (existing is CloudMistRendererFeature mist) feature = mist;
            }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<CloudMistRendererFeature>();
                feature.name = "CloudMist";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var serialized = new SerializedObject(renderer);
                var features = serialized.FindProperty("m_RendererFeatures");
                var map = serialized.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            SetField(feature, "_quality", quality);
            SetField(feature, "_material", material);
            feature.Create();
            EditorUtility.SetDirty(renderer);
        }

        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            EnsureDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
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

        private static void EnsureDirectory(string? directory)
        {
            if (string.IsNullOrEmpty(directory) || AssetDatabase.IsValidFolder(directory)) return;
            EnsureDirectory(Path.GetDirectoryName(directory)?.Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(directory)!.Replace('\\', '/'), Path.GetFileName(directory));
        }
    }
}
