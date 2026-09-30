#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.UI;
using TinCan.Core.UI.Commands;
using TinCan.Features.Voyage;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Voyage > Build Assets: creates (or re-applies) every asset of the voyage session, so its data is
    /// reviewable as code:
    /// <list type="bullet">
    /// <item>the config (tuning), the two end-screen menus and the beacon material;</item>
    /// <item>the VoyageState prefab and its ship fixture;</item>
    /// <item>the installer, and the profiles that load it (the sandbox and the Test_Voyage area).</item>
    /// </list>
    /// Scriptable objects are re-applied on every run. The prefab is built only when missing (delete it to rebuild it),
    /// so its network id stays stable. Plan: .docs/plans/voyage-session.md.
    /// </summary>
    public static class VoyageAssetBuilder
    {
        private const string Menus = "Assets/UI/Menus/";
        private const string StatePrefab = "Assets/Prefabs/Voyage/VoyageState.prefab";
        private const string Profiles = "Assets/Settings/FeatureProfiles/";
        private const string Installers = "Assets/Resources/Installers/";

        [MenuItem("TinCan/Dev/Voyage/Build Assets")]
        public static void Build()
        {
            var arrived = Menu("Menu_VoyageArrived", "voyage_arrived", "Voyage complete! The ship made it.");
            var lost = Menu("Menu_VoyageLost", "voyage_lost", "The ship is lost.");
            var beacon = Material("Assets/Materials/Voyage/M_VoyageBeacon.mat", new Color(1f, 0.8f, 0.25f));

            var config = Asset<VoyageConfig>("Assets/Settings/Voyage/VoyageConfig.asset", voyage =>
            {
                voyage.ArrivedMenu = arrived;
                voyage.LostMenu = lost;
                voyage.BeaconMaterial = beacon;
            });

            var fixture = Asset<ShipFixtureDefinition>("Assets/Settings/Fixtures/VoyageStateFixture.asset", definition =>
            {
                definition.Prefab = BuildStatePrefab();
                definition.LocalPosition = Vector3.zero;
                definition.LocalEulerAngles = Vector3.zero;
            });

            var installer = Asset<VoyageFeatureInstaller>(Installers + "VoyageFeatureInstaller.asset", voyage =>
            {
                SetField(voyage, "_config", config);
                SetField(voyage, "_stateFixture", fixture);
            });

            AddUnique(Load<FeatureProfile>(Profiles + "Profile_FuelSandbox.asset"), "_installers", new Object[] { installer });

            // The test area: the core test range plus everything a voyage paces (fuel, damage, the cannon, hazards), and
            // what those build on (ship damage references designed events).
            var test = Asset<FeatureProfile>(Profiles + "Test/Profile_Test_Voyage.asset", _ => { });
            AddUnique(test, "_includes", new Object[] { Load<FeatureProfile>(Profiles + "Test/Profile_Test_Core.asset") });
            AddUnique(test, "_installers", new Object[]
            {
                Load<Object>(Installers + "FuelFeatureInstaller.asset"),
                Load<Object>(Installers + "EventsFeatureInstaller.asset"),
                Load<Object>(Installers + "ShipDamageFeatureInstaller.asset"),
                Load<Object>(Installers + "StationsFeatureInstaller.asset"),
                Load<Object>(Installers + "CannonFeatureInstaller.asset"),
                Load<Object>(Installers + "SkyHazardsFeatureInstaller.asset"),
                installer
            });

            AssetDatabase.SaveAssets();
            Debug.Log("[VoyageAssetBuilder] Voyage assets built.");
        }

        private static MenuDefinition Menu(string file, string id, string title) =>
            Asset<MenuDefinition>(Menus + file + ".asset", menu =>
            {
                var serialized = new SerializedObject(menu);
                serialized.FindProperty("_menuId").stringValue = id;
                serialized.FindProperty("_title").stringValue = title;
                var items = serialized.FindProperty("_items");
                items.arraySize = 2;
                SetItem(items.GetArrayElementAtIndex(0), "restart", "Restart Voyage", RestartVoyageMenuCommand.Id);
                SetItem(items.GetArrayElementAtIndex(1), "quit", "Quit", QuitMenuCommand.Id);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

        private static void SetItem(SerializedProperty item, string id, string label, string command)
        {
            item.FindPropertyRelative(nameof(MenuItemDefinition.ItemId)).stringValue = id;
            item.FindPropertyRelative(nameof(MenuItemDefinition.Label)).stringValue = label;
            item.FindPropertyRelative(nameof(MenuItemDefinition.Kind)).enumValueIndex = (int)MenuItemKind.Command;
            item.FindPropertyRelative(nameof(MenuItemDefinition.CommandId)).stringValue = command;
            item.FindPropertyRelative(nameof(MenuItemDefinition.Submenu)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(MenuItemDefinition.DefaultValue)).stringValue = string.Empty;
        }

        private static GameObject BuildStatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatePrefab);
            if (existing != null) return existing;

            var root = new GameObject("VoyageState");
            root.AddComponent<NetworkObject>();
            root.AddComponent<EntityNetworkMediator>();
            root.AddComponent<VoyageNetworkMediator>();

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

        private static Material Material(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            EnsureFolder(path);
            AssetDatabase.CreateAsset(material, path);
            return material;
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
