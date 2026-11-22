#nullable enable
using System;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Features.CloudFlight;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Cloud Flight > Build Assets: creates (or re-applies) the cloud flight's assets (plan
    /// .docs/plans/cloud-sea.md, "Soaring and cloud shelves"): its config (made only when missing, so Inspector tuning
    /// survives a rebuild), its installer, and the profiles that load it (the sandbox, so the Galleon and cheesed_scene,
    /// and Test_Look for its scenario).
    /// </summary>
    public static class CloudFlightAssetBuilder
    {
        private const string ConfigPath = "Assets/Settings/CloudFlight/CloudFlightConfig.asset";
        private const string InstallerPath = "Assets/Resources/Installers/CloudFlightFeatureInstaller.asset";
        private const string Profiles = "Assets/Settings/FeatureProfiles/";
        private const string DashboardStylePath = "Assets/Scripts/Features/CloudFlight/CloudFlightDashboard.uss";

        [MenuItem("TinCan/Dev/Cloud Flight/Build Assets")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<CloudFlightConfig>(ConfigPath) ?? Create<CloudFlightConfig>(ConfigPath);
            var installer = AssetDatabase.LoadAssetAtPath<CloudFlightFeatureInstaller>(InstallerPath) ?? Create<CloudFlightFeatureInstaller>(InstallerPath);

            var serialized = new SerializedObject(installer);
            serialized.FindProperty("_config").objectReferenceValue = config;
            serialized.FindProperty("_dashboardStyle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(DashboardStylePath)
                ?? throw new InvalidOperationException($"Missing asset {DashboardStylePath}.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(installer);

            AddTo(Profiles + "Profile_FuelSandbox.asset", installer);
            AddTo(Profiles + "Test/Profile_Test_Look.asset", installer);

            AssetDatabase.SaveAssets();
            Debug.Log("[CloudFlightAssetBuilder] Cloud flight assets built.");
        }

        private static T Create<T>(string path) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            EnsureDirectory(Path.GetDirectoryName(path)!.Replace('\\', '/'));
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void AddTo(string profilePath, Object installer)
        {
            var profile = AssetDatabase.LoadAssetAtPath<FeatureProfile>(profilePath) ?? throw new InvalidOperationException($"Missing asset {profilePath}.");
            var serialized = new SerializedObject(profile);
            var list = serialized.FindProperty("_installers");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == installer) return;
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = installer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void EnsureDirectory(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            EnsureDirectory(Path.GetDirectoryName(folder)!.Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)!.Replace('\\', '/'), Path.GetFileName(folder));
        }
    }
}
