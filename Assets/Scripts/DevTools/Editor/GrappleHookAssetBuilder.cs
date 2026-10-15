#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Gas;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using TinCan.DevTools.Scenarios;
using TinCan.Features.GrappleHook;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Grapple Hook > Build Assets: every asset of the grappling hook, so its data is reviewable as code: the
    /// carrying tag and effect, the item, its held visual, the rack (IA_TakeGrappleHook) and the GrappleHookState prefab,
    /// their fixtures, the line and aim-marker materials, the config, the installer, its place in Profile_FuelSandbox and in
    /// Profile_Test_GrappleHook, and that area's test scene. Scriptable objects are re-applied on every run; prefabs and
    /// the scene are built only when missing (delete one to rebuild it), so network ids stay stable. Plan:
    /// .docs/plans/grapple-hook.md.
    /// </summary>
    public static class GrappleHookAssetBuilder
    {
        private const string Tags = "Assets/Abilities/Tags/";
        private const string Effects = "Assets/Abilities/Effects/";
        private const string Materials = "Assets/Materials/GrappleHook/";
        private const string ItemPath = "Assets/Items/ITEM_GrappleHook.asset";
        private const string InteractionPath = "Assets/Interactions/IA_TakeGrappleHook.asset";
        private const string HeldVisualPath = "Assets/Prefabs/Items/Carry_GrappleHook.prefab";
        private const string RackPrefabPath = "Assets/Prefabs/GrappleHook/GrappleHookRack.prefab";
        private const string StatePrefabPath = "Assets/Prefabs/GrappleHook/GrappleHookState.prefab";
        private const string RackFixturePath = "Assets/Settings/Fixtures/GrappleHookRackFixture.asset";
        private const string StateFixturePath = "Assets/Settings/Fixtures/GrappleHookStateFixture.asset";
        private const string ConfigPath = "Assets/Settings/GrappleHook/GrappleHookConfig.asset";
        private const string InstallerPath = "Assets/Resources/Installers/GrappleHookFeatureInstaller.asset";
        private const string MainProfilePath = "Assets/Settings/FeatureProfiles/Profile_FuelSandbox.asset";
        private const string TestProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_GrappleHook.asset";
        private const string TestCoreProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Core.asset";
        private const string PrimaryInputPath = "Assets/Abilities/Inputs/Input_Primary.asset";

        // Stable network id of the hook item: 1-3 are the jerry can, the net and the repair tool.
        private const int HookItemId = 4;

        // On the mid deck (top y -3.49 on the airship and the test ship), 1.5 m aft of the repair-tool rack on the same
        // side and facing the same way. Please check it against the airship's props in the Inspector.
        private static readonly Vector3 RackLocalPosition = new(-3f, -3.49f, 2.5f);
        private static readonly Vector3 RackLocalEuler = new(0f, 90f, 0f);

        // Under the player's Visual (origin at the capsule centre, eyes about 0.7 m up): low and to the right of the view.
        private static readonly Vector3 HeldLocalPosition = new(0.35f, 0.25f, 0.6f);

        [MenuItem("TinCan/Dev/Grapple Hook/Build Assets")]
        public static void Build()
        {
            var carrying = Asset<GameplayTag>(Tags + "State.Carrying.GrappleHook.asset", _ => { });
            var holding = Asset<GameplayEffectDefinition>(Effects + "GE_Holding_GrappleHook.asset", effect =>
            {
                effect.DurationType = DurationType.Infinite;
                effect.DurationSeconds = 0f;
                effect.Modifiers = new List<AttributeModifier>();
                effect.GrantedTags = new List<GameplayTag> { carrying };
                effect.Cues ??= new List<GameplayTag>();
            });

            var metal = Material("M_GrappleHook_Metal", new Color(0.22f, 0.23f, 0.26f));
            var grip = Material("M_GrappleHook_Grip", new Color(0.42f, 0.27f, 0.15f));
            var rope = Material("M_GrappleLine", new Color(0.72f, 0.62f, 0.42f), unlit: true);
            var inRange = Material("M_GrappleAim_InRange", new Color(0.25f, 0.95f, 0.35f), unlit: true);
            var outOfRange = Material("M_GrappleAim_OutOfRange", new Color(0.95f, 0.2f, 0.15f), unlit: true);

            var heldVisual = HeldVisualPrefab(metal, grip);
            var item = Asset<ItemDefinition>(ItemPath, definition =>
            {
                var fields = new SerializedObject(definition);
                fields.FindProperty("_id").intValue = HookItemId;
                fields.FindProperty("_displayName").stringValue = "Grappling hook";
                fields.FindProperty("_heldVisual").objectReferenceValue = heldVisual;
                fields.FindProperty("_equippedEffect").objectReferenceValue = holding;
                fields.FindProperty("_grantedAbilities").arraySize = 0;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            var take = Asset<InteractionDefinition>(InteractionPath, definition =>
                SetField(definition, "_handlerTypeName", typeof(TakeItemInteractionHandler).AssemblyQualifiedName!));

            var rackFixture = Asset<ShipFixtureDefinition>(RackFixturePath, fixture =>
            {
                fixture.Prefab = RackPrefab(take, item, metal, grip);
                fixture.LocalPosition = RackLocalPosition;
                fixture.LocalEulerAngles = RackLocalEuler;
            });
            var stateFixture = Asset<ShipFixtureDefinition>(StateFixturePath, fixture =>
            {
                fixture.Prefab = StatePrefab();
                fixture.LocalPosition = Vector3.zero;
                fixture.LocalEulerAngles = Vector3.zero;
            });

            var config = Asset<GrappleHookConfig>(ConfigPath, c =>
            {
                c.CarryingTag = carrying;
                c.FireInput = Load<GameplayInput>(PrimaryInputPath);
                c.LineMaterial = rope;
                c.InRangeMaterial = inRange;
                c.OutOfRangeMaterial = outOfRange;
            });

            var installer = Asset<GrappleHookFeatureInstaller>(InstallerPath, i =>
            {
                SetField(i, "_config", config);
                SetField(i, "_rackFixture", rackFixture);
                SetField(i, "_stateFixture", stateFixture);
                SetList(i, "_items", item);
            });

            AddUnique(Load<FeatureProfile>(MainProfilePath), "_installers", installer);
            var testProfile = Asset<FeatureProfile>(TestProfilePath, _ => { });
            AddUnique(testProfile, "_includes", Load<FeatureProfile>(TestCoreProfilePath));
            AddUnique(testProfile, "_installers", installer);

            AssetDatabase.SaveAssets();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenes.GrappleHook) == null) TestRangeSceneBuilder.BuildArea(TestScenes.GrappleHook);
            Debug.Log("[GrappleHookAssetBuilder] Grapple hook assets are up to date.");
        }

        /// <summary>The hook in the holder's hands: a grip, a short barrel and a three-pronged hook, with the line's start at its tip.</summary>
        private static GameObject HeldVisualPrefab(Material metal, Material grip)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HeldVisualPath);
            if (existing != null) return existing;

            var root = new GameObject("Carry_GrappleHook");
            root.transform.localPosition = HeldLocalPosition;
            HookModel(root.transform, metal, grip);

            EnsureFolder(HeldVisualPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HeldVisualPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void HookModel(Transform parent, Material metal, Material grip)
        {
            Visual(PrimitiveType.Cylinder, "Grip", parent, new Vector3(0f, -0.06f, 0f), Vector3.zero, new Vector3(0.045f, 0.07f, 0.045f), grip);
            Visual(PrimitiveType.Cube, "Barrel", parent, new Vector3(0f, 0.02f, 0.12f), Vector3.zero, new Vector3(0.07f, 0.07f, 0.3f), metal);
            Visual(PrimitiveType.Cube, "Shank", parent, new Vector3(0f, 0.02f, 0.31f), Vector3.zero, new Vector3(0.025f, 0.025f, 0.1f), metal);
            for (int i = 0; i < 3; i++)
            {
                float roll = i * 120f;
                var prong = new GameObject($"Prong_{i}").transform;
                prong.SetParent(parent, false);
                prong.localPosition = new Vector3(0f, 0.02f, 0.35f);
                prong.localEulerAngles = new Vector3(0f, 0f, roll);
                Visual(PrimitiveType.Cube, "Barb", prong, new Vector3(0f, 0.035f, -0.02f), new Vector3(-40f, 0f, 0f), new Vector3(0.018f, 0.018f, 0.09f), metal);
            }

            var tip = new GameObject("HookTip").transform;
            tip.SetParent(parent, false);
            tip.localPosition = new Vector3(0f, 0.02f, 0.37f);
        }

        /// <summary>A post and board like the repair-tool rack, the hook hanging on it, and an Interact trigger.</summary>
        private static GameObject RackPrefab(InteractionDefinition take, ItemDefinition item, Material metal, Material grip)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RackPrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("GrappleHookRack");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            var rack = root.AddComponent<ItemRackNetworkMediator>();
            SetField(rack, "_interactionDefinition", take);
            SetField(rack, "_item", item);

            Visual(PrimitiveType.Cube, "Post", root.transform, new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(0.15f, 1.2f, 0.15f), grip);
            Visual(PrimitiveType.Cube, "Board", root.transform, new Vector3(0f, 1.05f, 0.1f), Vector3.zero, new Vector3(0.6f, 0.4f, 0.05f), grip);
            var display = new GameObject("Hook").transform;
            display.SetParent(root.transform, false);
            display.localPosition = new Vector3(0f, 1.05f, 0.2f);
            display.localEulerAngles = new Vector3(-90f, 0f, 0f);
            HookModel(display, metal, grip);
            Object.DestroyImmediate(display.Find("HookTip").gameObject);

            var volume = new GameObject("InteractVolume").AddComponent<BoxCollider>();
            volume.transform.SetParent(root.transform, false);
            volume.transform.localPosition = new Vector3(0f, 1f, 0f);
            volume.isTrigger = true;
            volume.size = new Vector3(1.2f, 2f, 1.2f);

            return SaveNetworkPrefab(root, RackPrefabPath);
        }

        /// <summary>The GrappleHookState fixture: a NetworkObject carrying the players' replicated lines.</summary>
        private static GameObject StatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatePrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("GrappleHookState");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            root.AddComponent<GrappleHookStateNetworkMediator>();
            return SaveNetworkPrefab(root, StatePrefabPath);
        }

        private static GameObject SaveNetworkPrefab(GameObject root, string path)
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

        private static Material Material(string name, Color color, bool unlit = false)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")
                         ?? throw new InvalidOperationException("URP shader not found.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            EnsureFolder(path);
            AssetDatabase.CreateAsset(material, path);
            return material;
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
