#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Interaction;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.ShipSockets;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Ship Sockets > Build Assets: IA_MountFitting, the sockets config, the ShipSocketsState prefab and
    /// fixture, the installer, the first fittings (the cannon station, from the Cannon feature; the tool rack, from Damage),
    /// and their place in Profile_Test_Shipyard (which also gains the Cannon). Run TinCan > Dev > Ship Designs > Build
    /// Assets first. Plan: .docs/plans/modular-airship-builder.md (S5).
    /// </summary>
    public static class ShipSocketsAssetBuilder
    {
        private const string InteractionPath = "Assets/Interactions/IA_MountFitting.asset";
        private const string ConfigPath = "Assets/Settings/ShipSockets/ShipSocketsConfig.asset";
        private const string StatePrefabPath = "Assets/Prefabs/ShipSockets/ShipSocketsState.prefab";
        private const string StateFixturePath = "Assets/Settings/Fixtures/ShipSocketsStateFixture.asset";
        private const string InstallerPath = "Assets/Resources/Installers/ShipSocketsFeatureInstaller.asset";
        private const string FittingsFolder = "Assets/Settings/ShipFittings/";
        private const string Installers = "Assets/Resources/Installers/";

        public const string CannonFittingId = "weapon.cannon";
        public const string ToolRackFittingId = "storage.tool_rack";

        [MenuItem("TinCan/Dev/Ship Sockets/Build Assets")]
        public static void Build()
        {
            var interaction = Asset<InteractionDefinition>(InteractionPath, definition =>
                SetField(definition, "_handlerTypeName", typeof(MountFittingInteractionHandler).AssemblyQualifiedName!));
            var config = Asset<ShipSocketsConfig>(ConfigPath, c => c.MountInteraction = interaction);
            var stateFixture = Asset<ShipFixtureDefinition>(StateFixturePath, fixture =>
            {
                fixture.Prefab = StatePrefab();
                fixture.LocalPosition = Vector3.zero;
                fixture.LocalEulerAngles = Vector3.zero;
            });
            var installer = Asset<ShipSocketsFeatureInstaller>(InstallerPath, i =>
            {
                SetField(i, "_config", config);
                SetField(i, "_stateFixture", stateFixture);
            });

            var cannon = Fitting("FITTING_CannonStation", CannonFittingId, "Cannon station", "Assets/Prefabs/Weapons/CannonStation.prefab");
            var rack = Fitting("FITTING_ToolRack", ToolRackFittingId, "Repair tool rack", "Assets/Prefabs/Airship/Parts/RepairToolRack.prefab");
            var cannonInstaller = Load<FeatureInstaller>(Installers + "CannonFeatureInstaller.asset");
            SetList(cannonInstaller, "_fittings", cannon);
            SetList(Load<FeatureInstaller>(Installers + "ShipDamageFeatureInstaller.asset"), "_fittings", rack);

            var profile = Load<FeatureProfile>(ShipDesignsAssetBuilder.ProfilePath);
            AddUnique(profile, "_installers", installer, cannonInstaller);

            AssetDatabase.SaveAssets();
            Debug.Log("[ShipSocketsAssetBuilder] Ship socket assets built.");
        }

        private static ShipFittingDefinition Fitting(string asset, string fittingId, string displayName, string prefabPath) =>
            Asset<ShipFittingDefinition>(FittingsFolder + asset + ".asset", fitting =>
            {
                var fields = new SerializedObject(fitting);
                fields.FindProperty("_fittingId").stringValue = fittingId;
                fields.FindProperty("_displayName").stringValue = displayName;
                fields.FindProperty("_prefab").objectReferenceValue = Load<GameObject>(prefabPath);
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

        /// <summary>The ShipSocketsState fixture: a NetworkObject carrying what is mounted in a ship's sockets.</summary>
        private static GameObject StatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatePrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("ShipSocketsState");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            root.AddComponent<ShipSocketsNetworkMediator>();

            EnsureFolder(StatePrefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StatePrefabPath);
            Object.DestroyImmediate(root);

            // A new NetworkObject prefab keeps a stale id until validated; a client would refuse the host.
            var saved = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(saved, null);
            EditorUtility.SetDirty(saved);
            EditorUtility.SetDirty(prefab);
            return prefab;
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

        private static void SetField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            (serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}."))
                .objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetField(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            (serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}."))
                .stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetList(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AddUnique(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            var present = new HashSet<Object>();
            for (int i = 0; i < property.arraySize; i++) present.Add(property.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (var value in values)
            {
                if (!present.Add(value)) continue;
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
