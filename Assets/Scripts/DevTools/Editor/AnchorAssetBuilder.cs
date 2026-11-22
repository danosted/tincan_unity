#nullable enable
using System;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Interaction;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.Anchor;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Anchor > Build Assets: IA_ToggleAnchor, the Anchor prefab (a winch with a chain that shows while the
    /// anchor is down), its socket fitting (ship.anchor), the installer, and its place in the game's profile
    /// (Profile_Galleon). Then run TinCan > Dev > Ship Sockets > Build Galleon Airship, which mounts it on the Galleon's
    /// quarterdeck. The prefab is built only when missing (delete it to rebuild it), so its network id stays stable. Plan:
    /// .docs/plans/peaceful-mode.md (P1).
    /// </summary>
    public static class AnchorAssetBuilder
    {
        public const string FittingId = "ship.anchor";

        private const string InteractionPath = "Assets/Interactions/IA_ToggleAnchor.asset";
        private const string PrefabPath = "Assets/Prefabs/Anchor/Anchor.prefab";
        private const string FittingPath = "Assets/Settings/ShipFittings/FITTING_Anchor.asset";
        private const string InstallerPath = "Assets/Resources/Installers/AnchorFeatureInstaller.asset";
        private const string WoodPath = "Assets/Materials/Respawn/M_LeashWinch_Wood.mat";
        private const string IronPath = "Assets/Materials/Respawn/M_LeashWinch_Iron.mat";

        [MenuItem("TinCan/Dev/Anchor/Build Assets")]
        public static void Build()
        {
            var toggle = Asset<InteractionDefinition>(InteractionPath, definition =>
            {
                var fields = new SerializedObject(definition);
                fields.FindProperty("_handlerTypeName").stringValue = typeof(ToggleAnchorInteractionHandler).AssemblyQualifiedName!;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            var prefab = AnchorPrefab(toggle, Load<Material>(WoodPath), Load<Material>(IronPath));
            var fitting = Asset<ShipFittingDefinition>(FittingPath, definition =>
            {
                var fields = new SerializedObject(definition);
                fields.FindProperty("_fittingId").stringValue = FittingId;
                fields.FindProperty("_displayName").stringValue = "Anchor";
                fields.FindProperty("_prefab").objectReferenceValue = prefab;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            var installer = Asset<AnchorFeatureInstaller>(InstallerPath, i =>
            {
                var fields = new SerializedObject(i);
                var list = fields.FindProperty("_fittings");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = fitting;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            var game = Load<FeatureProfile>(ShipSocketsAssetBuilder.GalleonProfilePath);
            var serialized = new SerializedObject(game);
            var installers = serialized.FindProperty("_installers");
            bool present = false;
            for (int i = 0; i < installers.arraySize; i++) present |= installers.GetArrayElementAtIndex(i).objectReferenceValue == installer;
            if (!present)
            {
                installers.arraySize++;
                installers.GetArrayElementAtIndex(installers.arraySize - 1).objectReferenceValue = installer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(game);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AnchorAssetBuilder] Anchor assets built; run Ship Sockets > Build Galleon Airship to mount it.");
        }

        /// <summary>
        /// The anchor: a deck winch (its root on the deck, facing out along +z) with a chain over the side that shows only
        /// while the anchor is down, and an Interact volume (IA_ToggleAnchor).
        /// </summary>
        private static GameObject AnchorPrefab(InteractionDefinition toggle, Material wood, Material iron)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("Anchor");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            var anchor = root.AddComponent<AnchorNetworkMediator>();
            var fields = new SerializedObject(anchor);
            fields.FindProperty("_interactionDefinition").objectReferenceValue = toggle;
            fields.ApplyModifiedPropertiesWithoutUndo();

            Visual(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(0.9f, 0.3f, 0.7f), wood);
            Visual(PrimitiveType.Cube, "Post_L", root.transform, new Vector3(-0.4f, 0.55f, 0f), Vector3.zero, new Vector3(0.12f, 0.8f, 0.12f), wood);
            Visual(PrimitiveType.Cube, "Post_R", root.transform, new Vector3(0.4f, 0.55f, 0f), Vector3.zero, new Vector3(0.12f, 0.8f, 0.12f), wood);
            Visual(PrimitiveType.Cylinder, "Drum", root.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.45f, 0.35f, 0.45f), iron);
            Visual(PrimitiveType.Cube, "Lever", root.transform, new Vector3(0.55f, 0.95f, 0f), new Vector3(30f, 0f, 0f), new Vector3(0.06f, 0.5f, 0.06f), iron);

            // The chain: from the drum out over the side and down; only shown while the anchor is down.
            var chain = new GameObject("Chain").transform;
            chain.SetParent(root.transform, false);
            Visual(PrimitiveType.Cylinder, "Run", chain, new Vector3(0f, 0.8f, 0.6f), new Vector3(90f, 0f, 0f), new Vector3(0.08f, 0.6f, 0.08f), iron);
            Visual(PrimitiveType.Cylinder, "Drop", chain, new Vector3(0f, -2.2f, 1.2f), Vector3.zero, new Vector3(0.08f, 3f, 0.08f), iron);
            Visual(PrimitiveType.Cube, "Fluke", chain, new Vector3(0f, -5.2f, 1.2f), new Vector3(0f, 0f, 45f), new Vector3(0.5f, 0.5f, 0.12f), iron);
            chain.gameObject.SetActive(false);

            var volume = new GameObject("InteractVolume").AddComponent<BoxCollider>();
            volume.transform.SetParent(root.transform, false);
            volume.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            volume.isTrigger = true;
            volume.size = new Vector3(1.2f, 1.8f, 1.2f);

            EnsureFolder(PrefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // The NetworkObject's id was hashed while it was a scene object; its validation re-hashes it from the asset, or
            // a client would refuse the host.
            var saved = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(saved, null);
            EditorUtility.SetDirty(saved);
            EditorUtility.SetDirty(prefab);
            return prefab;
        }

        private static void Visual(PrimitiveType shape, string name, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = name;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localEulerAngles = euler;
            visual.transform.localScale = scale;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
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
