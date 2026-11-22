#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Gas;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Gathering;
using TinCan.Features.IslandDetails;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Gathering > Build Assets: every asset of fuel gathering, reviewable as code: the fuel crystal (its
    /// carrying tag and effect, its held visual), IA_MineCrystal (held work: <see cref="MineSeconds"/>), the config, the
    /// vein prefab (warm amber crystals in ochre rock; its Crystal_* children are its stock), and the wiring: the veins in
    /// Island Details' config, the crystal in the fuel config (a crystal fed to the receiver is fuel), the installer in
    /// the game's profile and in Profile_Test_Islands. Run after Island Details' builder. Prefabs are built only when
    /// missing (delete one to rebuild it), so network ids stay stable. Placeholder models in the look's library
    /// materials; please review them in the Inspector. Plan: .docs/plans/fuel-gathering.md.
    /// </summary>
    public static class GatheringAssetBuilder
    {
        /// <summary>How long mining one crystal takes, seconds (the interaction's hold time).</summary>
        public const float MineSeconds = 2.5f;

        /// <summary>The vein's interaction target name, for scenarios.</summary>
        public const string VeinTargetName = "Vein_Amber";

        private const string Tags = "Assets/Abilities/Tags/";
        private const string Effects = "Assets/Abilities/Effects/";
        private const string ItemPath = "Assets/Items/ITEM_FuelCrystal.asset";
        private const string HeldVisualPath = "Assets/Prefabs/Items/Carry_FuelCrystal.prefab";
        private const string InteractionPath = "Assets/Interactions/IA_MineCrystal.asset";
        private const string VeinPath = "Assets/Prefabs/Gathering/" + VeinTargetName + ".prefab";
        private const string ConfigPath = "Assets/Settings/Gathering/GatheringConfig.asset";
        private const string InstallerPath = "Assets/Resources/Installers/GatheringFeatureInstaller.asset";
        private const string IslandDetailsConfigPath = "Assets/Settings/IslandDetails/IslandDetailsConfig.asset";
        private const string FuelConfigPath = "Assets/Settings/FuelConfig.asset";
        private const string TestIslandsProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Islands.asset";

        // Stable network ids: 1-7 are the jerry can, the net, the repair tool, the hook, firewood, the tent roll, the bundle.
        private const int CrystalItemId = 8;

        // Under the player's Visual (origin at the capsule centre): low in front, in both hands.
        private static readonly Vector3 HeldLocalPosition = new(0f, -0.15f, 0.5f);

        [MenuItem("TinCan/Dev/Gathering/Build Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[GatheringAssetBuilder] Stop Play mode before building the gathering assets.");
                return;
            }

            var crystalMaterial = LookMaterials.Get("M_FuelCrystal");
            var rock = LookMaterials.Get("M_ShipCairn");

            var crystal = CrystalItem(HeldVisual(crystalMaterial));
            // The carrying tag resolves by name through the database: a tag missing from it is never held.
            // Saved explicitly: a database refreshed by the import postprocessor can stay changed in memory only.
            var tags = Load<GameplayTagDatabase>("Assets/Abilities/GameplayTagDatabase.asset");
            tags.Refresh();
            EditorUtility.SetDirty(tags);
            AssetDatabase.SaveAssetIfDirty(tags);

            var mine = Asset<InteractionDefinition>(InteractionPath, definition =>
            {
                var fields = new SerializedObject(definition);
                fields.FindProperty("_handlerTypeName").stringValue = typeof(MineCrystalInteractionHandler).AssemblyQualifiedName!;
                fields.FindProperty("_holdSeconds").floatValue = MineSeconds;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            var config = Asset<GatheringConfig>(ConfigPath, c => c.CrystalItem = crystal);
            var vein = VeinPrefab(mine, config, crystalMaterial, rock);

            var installer = Asset<GatheringFeatureInstaller>(InstallerPath, i =>
            {
                var fields = new SerializedObject(i);
                var items = fields.FindProperty("_items");
                items.arraySize = 1;
                items.GetArrayElementAtIndex(0).objectReferenceValue = crystal;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            Asset<IslandDetailsConfig>(IslandDetailsConfigPath, c => c.VeinPrefabs = new List<GameObject> { vein });
            Asset<FuelConfig>(FuelConfigPath, c => c.CrystalItem = crystal);
            AddUnique(Load<FeatureProfile>(ShipSocketsAssetBuilder.GalleonProfilePath), "_installers", installer);
            AddUnique(Load<FeatureProfile>(TestIslandsProfilePath), "_installers", installer);

            AssetDatabase.SaveAssets();
            Debug.Log("[GatheringAssetBuilder] Gathering assets are up to date.");
        }

        private static ItemDefinition CrystalItem(GameObject heldVisual)
        {
            var carrying = Asset<GameplayTag>(Tags + "State.Carrying.FuelCrystal.asset", _ => { });
            var holding = Asset<GameplayEffectDefinition>(Effects + "GE_Holding_FuelCrystal.asset", effect =>
            {
                effect.DurationType = DurationType.Infinite;
                effect.DurationSeconds = 0f;
                effect.Modifiers = new List<AttributeModifier>();
                effect.GrantedTags = new List<GameplayTag> { carrying };
                effect.Cues ??= new List<GameplayTag>();
            });

            return Asset<ItemDefinition>(ItemPath, definition =>
            {
                var fields = new SerializedObject(definition);
                fields.FindProperty("_id").intValue = CrystalItemId;
                fields.FindProperty("_displayName").stringValue = "Fuel crystal";
                fields.FindProperty("_heldVisual").objectReferenceValue = heldVisual;
                fields.FindProperty("_equippedEffect").objectReferenceValue = holding;
                fields.FindProperty("_grantedAbilities").arraySize = 0;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        /// <summary>One chunky amber crystal held in front, a smaller one fused to its side.</summary>
        private static GameObject HeldVisual(Material crystal)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HeldVisualPath);
            if (existing != null) return existing;

            var root = new GameObject("Carry_FuelCrystal");
            root.transform.localPosition = HeldLocalPosition;
            Visual(PrimitiveType.Cube, "Crystal", root.transform, Vector3.zero, new Vector3(20f, 45f, 30f), new Vector3(0.16f, 0.3f, 0.16f), crystal);
            Visual(PrimitiveType.Cube, "Shard", root.transform, new Vector3(0.09f, -0.05f, 0.02f), new Vector3(-15f, 20f, -35f), new Vector3(0.08f, 0.16f, 0.08f), crystal);
            EnsureFolder(HeldVisualPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HeldVisualPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// A crystal vein: a low ochre outcrop (solid) with three amber crystals growing out of its top and front, which
        /// show while the vein holds them, and an Interact volume over it. Its front is +z.
        /// </summary>
        private static GameObject VeinPrefab(InteractionDefinition mine, GatheringConfig config, Material crystal, Material rock)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(VeinPath);
            if (existing != null) return existing;

            var root = new GameObject(VeinTargetName);
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            var vein = root.AddComponent<VeinNetworkMediator>();
            var fields = new SerializedObject(vein);
            fields.FindProperty("_interactionDefinition").objectReferenceValue = mine;
            fields.FindProperty("_config").objectReferenceValue = config;
            fields.ApplyModifiedPropertiesWithoutUndo();

            Visual(PrimitiveType.Sphere, "Rock", root.transform, new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(1.8f, 0.9f, 1.4f), rock);
            Visual(PrimitiveType.Sphere, "Rock_Shoulder", root.transform, new Vector3(-0.6f, 0.25f, -0.2f), Vector3.zero, new Vector3(0.9f, 0.6f, 0.9f), rock);
            Visual(PrimitiveType.Cube, "Crystal_0", root.transform, new Vector3(0.1f, 0.95f, 0.1f), new Vector3(10f, 30f, 15f), new Vector3(0.25f, 0.6f, 0.25f), crystal);
            Visual(PrimitiveType.Cube, "Crystal_1", root.transform, new Vector3(0.45f, 0.7f, 0.35f), new Vector3(-20f, 10f, -30f), new Vector3(0.2f, 0.45f, 0.2f), crystal);
            Visual(PrimitiveType.Cube, "Crystal_2", root.transform, new Vector3(-0.3f, 0.75f, 0.4f), new Vector3(25f, -20f, 20f), new Vector3(0.18f, 0.4f, 0.18f), crystal);

            var solid = root.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, 0.35f, 0f);
            solid.size = new Vector3(1.7f, 0.7f, 1.3f);

            var volume = new GameObject("InteractVolume").AddComponent<BoxCollider>();
            volume.transform.SetParent(root.transform, false);
            volume.transform.localPosition = new Vector3(0f, 0.9f, 0.2f);
            volume.isTrigger = true;
            volume.size = new Vector3(1.6f, 1.2f, 1.4f);

            EnsureFolder(VeinPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, VeinPath);
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
