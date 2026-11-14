#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Features.CloudBoundary;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Cloud Sea > Build Assets: creates (or re-applies) the drawn cloud sea's assets (plan
    /// .docs/plans/cloud-sea.md, "The drawn sea and its holes"):
    /// <list type="bullet">
    /// <item>the config, and the sea material (made only when missing, so Inspector tuning survives a rebuild);</item>
    /// <item>the CloudHolesState prefab and its ship fixture;</item>
    /// <item>the installer, and the profiles that load it (the sandbox, so the Galleon and cheesed_scene, and Test_Look).</item>
    /// </list>
    /// The prefab is built only when missing (delete it to rebuild it), so its network id stays stable.
    /// </summary>
    public static class CloudSeaAssetBuilder
    {
        public const string SeaShader = "TinCan/Environment/Cloud Sea Swell";
        public const string MaterialPath = "Assets/Materials/Clouds/M_CloudSeaSwell.mat";
        private const string StatePrefab = "Assets/Prefabs/CloudSea/CloudHolesState.prefab";
        private const string Profiles = "Assets/Settings/FeatureProfiles/";
        private const string Installers = "Assets/Resources/Installers/";

        [MenuItem("TinCan/Dev/Cloud Sea/Build Assets")]
        public static void Build()
        {
            var config = Asset<CloudSeaConfig>("Assets/Settings/CloudSea/CloudSeaConfig.asset", _ => { });
            var material = SeaMaterial();

            var fixture = Asset<ShipFixtureDefinition>("Assets/Settings/Fixtures/CloudHolesStateFixture.asset", definition =>
            {
                definition.Prefab = BuildStatePrefab();
                definition.LocalPosition = Vector3.zero;
                definition.LocalEulerAngles = Vector3.zero;
            });

            var installer = Asset<CloudSeaFeatureInstaller>(Installers + "CloudSeaFeatureInstaller.asset", sea =>
            {
                SetField(sea, "_config", config);
                SetField(sea, "_seaMaterial", material);
                SetField(sea, "_stateFixture", fixture);
            });

            AddUnique(Load<FeatureProfile>(Profiles + "Profile_FuelSandbox.asset"), "_installers", new Object[] { installer });
            AddUnique(Load<FeatureProfile>(Profiles + "Test/Profile_Test_Look.asset"), "_installers", new Object[] { installer });

            AssetDatabase.SaveAssets();
            Debug.Log("[CloudSeaAssetBuilder] Cloud sea assets built.");
        }

        private static Material SeaMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            var shader = Shader.Find(SeaShader) ?? throw new InvalidOperationException($"Missing shader {SeaShader}.");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(MaterialPath) };
            EnsureFolder(MaterialPath);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static GameObject BuildStatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatePrefab);
            if (existing != null) return existing;

            var root = new GameObject("CloudHolesState");
            root.AddComponent<NetworkObject>();
            root.AddComponent<EntityNetworkMediator>();
            root.AddComponent<CloudHolesNetworkMediator>();

            EnsureFolder(StatePrefab);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StatePrefab);
            Object.DestroyImmediate(root);

            // The NetworkObject's id was hashed while it was a scene object; its validation re-hashes it from the asset.
            var networkObject = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            return prefab;
        }

        /// <summary>Loads the asset at <paramref name="path"/> or creates it, applies <paramref name="configure"/>, and marks it dirty.</summary>
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

        private static void SetField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddUnique(Object target, string field, IEnumerable<Object> values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            foreach (var value in values)
            {
                bool present = false;
                for (int i = 0; i < property.arraySize; i++) present |= property.GetArrayElementAtIndex(i).objectReferenceValue == value;
                if (present) continue;

                property.arraySize++;
                property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

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
