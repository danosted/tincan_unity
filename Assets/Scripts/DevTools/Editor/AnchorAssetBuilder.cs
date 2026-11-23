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
    /// TinCan > Dev > Anchor > Build Assets: IA_ToggleAnchor, the Anchor prefab (a winch with a chain and a rope ladder
    /// that show while the anchor is down), its socket fitting (ship.anchor), the installer, and its place in the game's profile
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
                fields.FindProperty("_handlerTypeName").stringValue = typeof(ToggleInteractionHandler).AssemblyQualifiedName!;
                fields.FindProperty("_prompt").stringValue = "Anchor"; // the anchor says "Drop anchor" or "Raise anchor" itself
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            var wood = Load<Material>(WoodPath);
            var iron = Load<Material>(IronPath);
            var prefab = AnchorPrefab(toggle, wood, iron);
            Refit(prefab, wood, iron);
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

        // The plate stands this far inboard of the rail (the Galleon's quarterdeck, port aft), facing out over it.
        private const float RailOut = 1.5f;
        private const float LadderOut = 1.85f;
        private const float LadderLength = 13f;
        private const float LadderRungSpacing = 0.45f;

        /// <summary>
        /// Brings an existing anchor prefab up to date in place (its GUID and network id stay): the chain hangs over the
        /// side rather than through the deck, and a rope ladder ("Ladder", shown while the anchor is down) hangs beside it,
        /// hooked on the rail and reaching <see cref="LadderLength"/> below the deck. Its markers stay active under the
        /// root: "LadderTop" and "LadderFoot" (the line a climber's feet follow, just outside the rungs) and "LadderDeck"
        /// (inboard of the winch, facing in, where a climber lands).
        /// </summary>
        private static void Refit(GameObject prefab, Material wood, Material iron)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var chain = root.transform.Find("Chain");
                if (chain != null)
                {
                    Place(chain, "Run", new Vector3(0.7f, 0.8f, LadderOut / 2f), new Vector3(90f, 0f, 0f), new Vector3(0.08f, LadderOut / 2f, 0.08f));
                    Place(chain, "Drop", new Vector3(0.7f, -2.2f, LadderOut), Vector3.zero, new Vector3(0.08f, 3f, 0.08f));
                    Place(chain, "Fluke", new Vector3(0.7f, -5.2f, LadderOut), new Vector3(0f, 0f, 45f), new Vector3(0.5f, 0.5f, 0.12f));
                }

                var old = root.transform.Find("Ladder");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var ladder = new GameObject("Ladder").transform;
                ladder.SetParent(root.transform, false);
                const float hookHeight = 1.1f;
                float bottom = -LadderLength;
                float ropeHalf = (hookHeight - bottom) / 2f;
                foreach (float side in new[] { -0.28f, 0.28f })
                {
                    Visual(PrimitiveType.Cylinder, side < 0f ? "Rope_L" : "Rope_R", ladder, new Vector3(side, hookHeight - ropeHalf, LadderOut), Vector3.zero, new Vector3(0.05f, ropeHalf, 0.05f), wood);
                    Visual(PrimitiveType.Cube, side < 0f ? "Hook_L" : "Hook_R", ladder, new Vector3(side, hookHeight, (RailOut + LadderOut) / 2f), Vector3.zero, new Vector3(0.06f, 0.06f, LadderOut - RailOut + 0.1f), iron);
                }

                int rung = 0;
                for (float y = hookHeight - 0.4f; y > bottom + 0.1f; y -= LadderRungSpacing)
                    Visual(PrimitiveType.Cube, $"Rung_{rung++:00}", ladder, new Vector3(0f, y, LadderOut), Vector3.zero, new Vector3(0.6f, 0.05f, 0.07f), wood);
                ladder.gameObject.SetActive(false);

                Marker(root.transform, "LadderTop", new Vector3(0f, 0f, LadderOut + 0.45f), Vector3.zero);
                Marker(root.transform, "LadderFoot", new Vector3(0f, bottom, LadderOut + 0.45f), Vector3.zero);
                Marker(root.transform, "LadderDeck", new Vector3(0f, 0.05f, -0.9f), new Vector3(0f, 180f, 0f));

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Place(Transform parent, string name, Vector3 position, Vector3 euler, Vector3 scale)
        {
            var child = parent.Find(name);
            if (child == null) return;
            child.localPosition = position;
            child.localEulerAngles = euler;
            child.localScale = scale;
        }

        private static void Marker(Transform parent, string name, Vector3 position, Vector3 euler)
        {
            var marker = parent.Find(name) ?? new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.localPosition = position;
            marker.localEulerAngles = euler;
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
