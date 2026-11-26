#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.UI;
using TinCan.Core.UI.Commands;
using TinCan.Features.ShipLoss;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Ship Loss > Build Assets: creates (or re-applies) every asset of ship loss, so its data is
    /// reviewable as code:
    /// <list type="bullet">
    /// <item>the Lost screen (Respawn, the choices another feature adds, Quit);</item>
    /// <item>the ShipLossState prefab and its ship fixture;</item>
    /// <item>the installer, and the profiles that load it: the game's base, and the test areas that run a voyage (the
    /// voyage builds on it). Run after TinCan > Dev > Voyage > Build Assets on a fresh project.</item>
    /// </list>
    /// Scriptable objects are re-applied on every run. The prefab is built only when missing (delete it to rebuild it),
    /// so its network id stays stable. Plan: .docs/plans/ship-lost-and-respawn.md.
    /// </summary>
    public static class ShipLossAssetBuilder
    {
        private const string StatePrefab = "Assets/Prefabs/ShipLoss/ShipLossState.prefab";
        private const string Profiles = "Assets/Settings/FeatureProfiles/";
        private const string Installers = "Assets/Resources/Installers/";

        [MenuItem("TinCan/Dev/Ship Loss/Build Assets")]
        public static void Build()
        {
            var lost = Asset<MenuDefinition>("Assets/UI/Menus/Menu_ShipLost.asset", menu =>
            {
                var serialized = new SerializedObject(menu);
                serialized.FindProperty("_menuId").stringValue = ShipLossMenus.MenuId;
                serialized.FindProperty("_title").stringValue = "The ship is lost, and its whole crew.";
                var items = serialized.FindProperty("_items");
                items.arraySize = 3;
                SetItem(items.GetArrayElementAtIndex(0), ShipLossMenus.RespawnItem, "Respawn", MenuItemKind.Command, RespawnShipMenuCommand.Id);
                // No label: the choices show as plain rows (none without a feature that adds them).
                SetItem(items.GetArrayElementAtIndex(1), "choices", string.Empty, MenuItemKind.Rows, string.Empty, ShipLossMenus.ChoicesSource);
                SetItem(items.GetArrayElementAtIndex(2), "quit", "Quit", MenuItemKind.Command, QuitMenuCommand.Id);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

            var fixture = Asset<ShipFixtureDefinition>("Assets/Settings/Fixtures/ShipLossStateFixture.asset", definition =>
            {
                definition.Prefab = BuildStatePrefab();
                definition.LocalPosition = Vector3.zero;
                definition.LocalEulerAngles = Vector3.zero;
            });

            var installer = Asset<ShipLossFeatureInstaller>(Installers + "ShipLossFeatureInstaller.asset", loss =>
            {
                SetField(loss, "_lostMenu", lost);
                SetField(loss, "_stateFixture", fixture);
            });

            foreach (var profile in new[] { "Profile_Base", "Test/Profile_Test_Voyage", "Test/Profile_Test_Navigation" })
                AddUnique(Load<FeatureProfile>(Profiles + profile + ".asset"), "_installers", installer);

            AssetDatabase.SaveAssets();
            Debug.Log("[ShipLossAssetBuilder] Ship loss assets built.");
        }

        private static void SetItem(SerializedProperty item, string id, string label, MenuItemKind kind, string command, string rowSource = "")
        {
            item.FindPropertyRelative(nameof(MenuItemDefinition.ItemId)).stringValue = id;
            item.FindPropertyRelative(nameof(MenuItemDefinition.Label)).stringValue = label;
            item.FindPropertyRelative(nameof(MenuItemDefinition.Kind)).enumValueIndex = (int)kind;
            item.FindPropertyRelative(nameof(MenuItemDefinition.CommandId)).stringValue = command;
            item.FindPropertyRelative(nameof(MenuItemDefinition.Submenu)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(MenuItemDefinition.DefaultValue)).stringValue = string.Empty;
            item.FindPropertyRelative(nameof(MenuItemDefinition.RowSource)).stringValue = rowSource;
        }

        private static GameObject BuildStatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatePrefab);
            if (existing != null) return existing;

            var root = new GameObject("ShipLossState");
            root.AddComponent<NetworkObject>();
            root.AddComponent<EntityNetworkMediator>();
            root.AddComponent<ShipLossNetworkMediator>();

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

        private static void AddUnique(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
            }

            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = value;
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
