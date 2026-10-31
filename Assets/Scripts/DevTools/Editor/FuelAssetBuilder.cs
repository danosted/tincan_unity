#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.Airship.Fuel;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Fuel > Build Fittings: fuel through the sockets (plan .docs/plans/main-game-state.md, M2).
    /// <list type="bullet">
    /// <item>the tank becomes ship state: the pose-less FuelTankState prefab and fixture, on every ship; FuelSystem loses
    /// its tank and keeps the receiver, the crystal rack and the gauge for the old hull;</item>
    /// <item>the fittings fuel.receiver (the receiver with the gauge beside it) and fuel.crystal_rack, made from
    /// FuelSystem's children, and fuel.net_rack (the existing NetRack prefab);</item>
    /// <item>both installers list them. Each crystal rack reads FuelConfig itself.</item>
    /// </list>
    /// Prefabs are built only when missing (delete one to rebuild it), so their network ids stay stable. Every fitting's
    /// pivot is at its base and it faces its socket's +Z; the Galleon's aft hold plates face inboard.
    /// </summary>
    public static class FuelAssetBuilder
    {
        private const string Parts = "Assets/Prefabs/Airship/Parts/";
        private const string FuelSystemPath = Parts + "FuelSystem.prefab";
        private const string NetRackPath = Parts + "NetRack.prefab";
        private const string TankStatePath = Parts + "FuelTankState.prefab";
        private const string ReceiverPath = Parts + "FuelReceiver.prefab";
        private const string CrystalRackPath = Parts + "CrystalRack.prefab";
        private const string TankFixturePath = "Assets/Settings/Fixtures/FuelTankStateFixture.asset";
        private const string ConfigPath = "Assets/Settings/FuelConfig.asset";
        private const string FuelAttributePath = "Assets/Abilities/Attributes/Attr_Fuel.asset";
        private const string Fittings = "Assets/Settings/ShipFittings/";
        private const string Installers = "Assets/Resources/Installers/";

        // The gauge stands beside the receiver along its long side, unturned, so its dial faces the fitting's front (+Z);
        // on the old hull it was turned 180 degrees to face the receiver from forward of it. Please check in the Inspector.
        private static readonly Vector3 GaugeLocalPosition = new(1.9f, 0f, 0f);

        [MenuItem("TinCan/Dev/Fuel/Build Fittings")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[FuelAssetBuilder] Stop Play mode before building the fuel fittings.");
                return;
            }

            var config = Load<FuelConfig>(ConfigPath);
            var tankState = TankStatePrefab(config, Load<Object>(FuelAttributePath));
            var receiver = ReceiverPrefab();
            var crystalRack = CrystalRackPrefab(config);
            StripTankFromFuelSystem(config);

            var tankFixture = Asset<ShipFixtureDefinition>(TankFixturePath, fixture =>
            {
                fixture.Prefab = tankState;
                fixture.LocalPosition = Vector3.zero;
                fixture.LocalEulerAngles = Vector3.zero;
            });
            var receiverFitting = Fitting("FITTING_FuelReceiver", ShipSocketsAssetBuilder.FuelReceiverFittingId, "Fuel receiver", receiver);
            var rackFitting = Fitting("FITTING_CrystalRack", ShipSocketsAssetBuilder.CrystalRackFittingId, "Crystal rack", crystalRack);
            var netFitting = Fitting("FITTING_NetRack", ShipSocketsAssetBuilder.NetRackFittingId, "Net rack", Load<GameObject>(NetRackPath));

            var fuel = Load<FeatureInstaller>(Installers + "FuelFeatureInstaller.asset");
            SetField(fuel, "_tankStateFixture", tankFixture);
            SetList(fuel, "_fittings", receiverFitting, rackFitting);
            SetList(Load<FeatureInstaller>(Installers + "FlyingCanFeatureInstaller.asset"), "_fittings", netFitting);

            AssetDatabase.SaveAssets();
            Debug.Log("[FuelAssetBuilder] Fuel fittings built.");
        }

        /// <summary>The ship's tank on its own pose-less NetworkObject: it binds to the ship it is parented to.</summary>
        private static GameObject TankStatePrefab(FuelConfig config, Object fuelAttribute)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TankStatePath);
            if (existing != null) return existing;

            var root = NetworkedRoot("FuelTankState");
            var tank = root.AddComponent<FuelTankNetworkMediator>();
            var fields = new SerializedObject(tank);
            fields.FindProperty("_config").objectReferenceValue = config;
            fields.FindProperty("_fuelAttribute").objectReferenceValue = fuelAttribute;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, TankStatePath);
        }

        /// <summary>FuelSystem's MotorFillPort (receiver) at the pivot and its FuelGauge beside it.</summary>
        private static GameObject ReceiverPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ReceiverPath);
            if (existing != null) return existing;

            var root = NetworkedRoot("FuelReceiver");
            CopyFromFuelSystem("MotorFillPort", root.transform, Vector3.zero, Quaternion.identity);
            CopyFromFuelSystem("FuelGauge", root.transform, GaugeLocalPosition, Quaternion.identity);
            return Save(root, ReceiverPath);
        }

        /// <summary>FuelSystem's JerryCanSupply (the crystal rack and its three crystals) at the pivot.</summary>
        private static GameObject CrystalRackPrefab(FuelConfig config)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalRackPath);
            if (existing != null) return existing;

            var root = NetworkedRoot("CrystalRack");
            var supply = CopyFromFuelSystem("JerryCanSupply", root.transform, Vector3.zero, Quaternion.identity);
            SetField(supply.GetComponent<JerryCanSupplyNetworkMediator>(), "_config", config);
            return Save(root, CrystalRackPath);
        }

        /// <summary>The old hull's FuelSystem keeps its parts but not the tank (FuelTankState holds it now).</summary>
        private static void StripTankFromFuelSystem(FuelConfig config)
        {
            var root = PrefabUtility.LoadPrefabContents(FuelSystemPath);
            try
            {
                var tank = root.GetComponent<FuelTankNetworkMediator>();
                if (tank != null) Object.DestroyImmediate(tank, true);
                foreach (var supply in root.GetComponentsInChildren<JerryCanSupplyNetworkMediator>(true)) SetField(supply, "_config", config);
                PrefabUtility.SaveAsPrefabAsset(root, FuelSystemPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject CopyFromFuelSystem(string child, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            var source = Load<GameObject>(FuelSystemPath).transform.Find(child)
                         ?? throw new InvalidOperationException($"{FuelSystemPath} has no child {child}.");
            var copy = Object.Instantiate(source.gameObject, parent, false);
            copy.name = child;
            copy.transform.localPosition = localPosition;
            copy.transform.localRotation = localRotation;
            return copy;
        }

        private static GameObject NetworkedRoot(string name)
        {
            var root = new GameObject(name);
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            return root;
        }

        private static GameObject Save(GameObject root, string path)
        {
            EnsureFolder(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            // The NetworkObject's id was hashed while it was a scene object; its validation re-hashes it from the asset,
            // or a client would refuse the host.
            var saved = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(saved, null);
            EditorUtility.SetDirty(saved);
            EditorUtility.SetDirty(prefab);
            return prefab;
        }

        private static ShipFittingDefinition Fitting(string file, string id, string displayName, GameObject prefab) =>
            Asset<ShipFittingDefinition>(Fittings + file + ".asset", fitting =>
            {
                var fields = new SerializedObject(fitting);
                fields.FindProperty("_fittingId").stringValue = id;
                fields.FindProperty("_displayName").stringValue = displayName;
                fields.FindProperty("_prefab").objectReferenceValue = prefab;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

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
            EditorUtility.SetDirty(target);
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
