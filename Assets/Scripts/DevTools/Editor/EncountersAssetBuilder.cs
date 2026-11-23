#nullable enable
using System;
using System.IO;
using TinCan.Core.Entities;
using TinCan.Features.Encounters;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Encounters > Build Assets: the first encounter zone (plan: .docs/plans/encounters.md, E1): its
    /// config (the storm, 750 m round, its wall 750 m north of the start), the zone prefab (built only when missing, so
    /// its network id stays) and its placeholder wall, a ring of tall plum pillars rebuilt from the config's radius on
    /// every build, and the zone on the Encounters installer (already in the game's profile).
    /// </summary>
    public static class EncountersAssetBuilder
    {
        private const string ConfigPath = "Assets/Settings/Encounters/EncounterZoneConfig.asset";
        private const string PrefabPath = "Assets/Prefabs/Encounters/EncounterZone.prefab";
        private const string InstallerPath = "Assets/Resources/Installers/EncountersFeatureInstaller.asset";
        private const string WallName = "Wall";
        private const int Pillars = 16;

        [MenuItem("TinCan/Dev/Encounters/Build Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[EncountersAssetBuilder] Stop Play mode before building the encounter assets.");
                return;
            }

            var config = Asset<EncounterZoneConfig>(ConfigPath, _ => { });
            var prefab = ZonePrefab(config);
            Wall(config.Radius, LookMaterials.Get("M_EncounterWall"));
            config.Prefab = prefab;
            EditorUtility.SetDirty(config);

            var installer = Load<EncountersFeatureInstaller>(InstallerPath);
            var fields = new SerializedObject(installer);
            fields.FindProperty("_zone").objectReferenceValue = config;
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(installer);

            AssetDatabase.SaveAssets();
            Debug.Log("[EncountersAssetBuilder] Encounter assets are up to date.");
        }

        private static GameObject ZonePrefab(EncounterZoneConfig config)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("EncounterZone");
            root.AddComponent<NetworkObject>();
            root.AddComponent<EntityNetworkMediator>();
            var zone = root.AddComponent<EncounterZoneNetworkMediator>();
            var fields = new SerializedObject(zone);
            fields.FindProperty("_config").objectReferenceValue = config;
            fields.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(PrefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // The NetworkObject's id was hashed while it was a scene object; its validation re-hashes it from the asset.
            var saved = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(saved, null);
            EditorUtility.SetDirty(saved);
            EditorUtility.SetDirty(prefab);
            return prefab;
        }

        /// <summary>
        /// The placeholder wall: <see cref="Pillars"/> tall plum pillars on the zone's edge, from below the cloud sea to well
        /// above cruising height, so the zone reads from afar. No colliders: ships sail through. Replaced each build.
        /// </summary>
        private static void Wall(float radius, Material material)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var old = root.transform.Find(WallName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var wall = new GameObject(WallName).transform;
                wall.SetParent(root.transform, false);
                for (int i = 0; i < Pillars; i++)
                {
                    float angle = i * Mathf.PI * 2f / Pillars;
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pillar.name = $"Pillar_{i:00}";
                    Object.DestroyImmediate(pillar.GetComponent<Collider>());
                    pillar.transform.SetParent(wall, false);
                    pillar.transform.localPosition = new Vector3(Mathf.Sin(angle) * radius, 100f, Mathf.Cos(angle) * radius);
                    pillar.transform.localScale = new Vector3(12f, 150f, 12f);
                    pillar.GetComponent<MeshRenderer>().sharedMaterial = material;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
