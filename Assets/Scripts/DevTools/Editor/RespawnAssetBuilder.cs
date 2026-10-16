#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;
using TinCan.Core.Entities;
using TinCan.Core.Gas;
using TinCan.Core.Interaction;
using TinCan.Core.Ship.Sockets;
using TinCan.DevTools.Scenarios;
using TinCan.Features.Respawn;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Respawn > Build Assets builds every asset of the overboard choice:
    /// - the State.Overboard and State.Leashed tags and their effects;
    /// - the rules asset (Settings/Respawn/RespawnRules, the one place for "what happens when a player respawns");
    /// - the RespawnState prefab and fixture, the leash winch (a leash point with a winch, IA_CrankWinch) and its fixture;
    /// - the Leashed input context (through the input builder, which also rewrites INPUT_MAP.md), and the installer;
    /// - its place in Profile_FuelSandbox and in Profile_Test_Respawn, and that area's scene.
    /// The rules' values are written only when the asset is created, so a designer's edits are kept. Plan:
    /// .docs/plans/overboard-and-rescue.md.
    /// </summary>
    public static class RespawnAssetBuilder
    {
        private const string TagPath = "Assets/Abilities/Tags/State.Overboard.asset";
        private const string EffectPath = "Assets/Abilities/Effects/GE_Overboard.asset";
        private const string RulesPath = "Assets/Settings/Respawn/RespawnRules.asset";
        private const string StatePrefabPath = "Assets/Prefabs/Respawn/RespawnState.prefab";
        private const string StateFixturePath = "Assets/Settings/Fixtures/RespawnStateFixture.asset";
        private const string InstallerPath = "Assets/Resources/Installers/RespawnFeatureInstaller.asset";
        private const string MainProfilePath = "Assets/Settings/FeatureProfiles/Profile_FuelSandbox.asset";
        private const string TestProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Respawn.asset";
        private const string TestCoreProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Core.asset";
        private const string GrappleHookInstallerPath = "Assets/Resources/Installers/GrappleHookFeatureInstaller.asset";
        private const string LeashedTagPath = "Assets/Abilities/Tags/State.Leashed.asset";
        private const string LeashedEffectPath = "Assets/Abilities/Effects/GE_Leashed.asset";
        private const string CrankInteractionPath = "Assets/Interactions/IA_CrankWinch.asset";
        private const string WinchPrefabPath = "Assets/Prefabs/Respawn/LeashWinch.prefab";
        private const string WinchFixturePath = "Assets/Settings/Fixtures/LeashWinchFixture.asset";
        private const string LeashedContextPath = "Assets/Input/Contexts/Context_Leashed.asset";
        private const string Materials = "Assets/Materials/Respawn/";
        private const string WinchFittingPath = "Assets/Settings/ShipFittings/FITTING_LeashWinch.asset";
        public const string WinchFittingId = "rescue.leash_winch";

        // On the mid deck (top y -3.49 on the airship and the test ship), just inboard of the starboard rail (the test
        // ship's is at x 6.3), facing out. The rope's anchor hangs past the rail, and a climbed-up player stands inboard.
        // Please check this spot against the airship's props in the Inspector.
        private static readonly Vector3 WinchLocalPosition = new(5.8f, -3.49f, 0f);
        private static readonly Vector3 WinchLocalEuler = new(0f, 90f, 0f);

        [MenuItem("TinCan/Dev/Respawn/Build Assets")]
        public static void Build()
        {
            var tag = Asset<GameplayTag>(TagPath, _ => { });
            var effect = Asset<GameplayEffectDefinition>(EffectPath, e =>
            {
                e.DurationType = DurationType.Infinite;
                e.DurationSeconds = 0f;
                e.Modifiers = new List<AttributeModifier>();
                e.GrantedTags = new List<GameplayTag> { tag };
                e.Cues ??= new List<GameplayTag>();
            });

            var leashedTag = Asset<GameplayTag>(LeashedTagPath, _ => { });
            var leashedEffect = Asset<GameplayEffectDefinition>(LeashedEffectPath, e =>
            {
                e.DurationType = DurationType.Infinite;
                e.DurationSeconds = 0f;
                e.Modifiers = new List<AttributeModifier>();
                e.GrantedTags = new List<GameplayTag> { leashedTag };
                e.Cues ??= new List<GameplayTag>();
            });
            var rope = Material("M_LeashRope", new Color(0.72f, 0.62f, 0.42f), unlit: true);
            var wood = Material("M_LeashWinch_Wood", new Color(0.45f, 0.3f, 0.18f));
            var iron = Material("M_LeashWinch_Iron", new Color(0.2f, 0.2f, 0.22f));

            bool rulesExisted = AssetDatabase.LoadAssetAtPath<RespawnRules>(RulesPath) != null;
            var rules = Asset<RespawnRules>(RulesPath, r =>
            {
                r.OverboardEffect = effect;
                r.OverboardTag = tag;
                r.LeashedEffect = leashedEffect;
                r.LeashedTag = leashedTag;
                if (r.RopeMaterial == null) r.RopeMaterial = rope;
                if (rulesExisted) return;

                // Development defaults: respawn as soon as you choose, and lose what you carried.
                r.Rules = new List<RespawnRule> { new() { Cause = RespawnCause.Overboard, CarriedItem = CarriedItemOutcome.Lose, DelaySeconds = 0f } };
            });

            var stateFixture = Asset<ShipFixtureDefinition>(StateFixturePath, fixture =>
            {
                fixture.Prefab = StatePrefab();
                fixture.LocalPosition = Vector3.zero;
                fixture.LocalEulerAngles = Vector3.zero;
            });
            var crank = Asset<InteractionDefinition>(CrankInteractionPath, definition =>
            {
                var fields = new SerializedObject(definition);
                fields.FindProperty("_handlerTypeName").stringValue = typeof(CrankWinchInteractionHandler).AssemblyQualifiedName!;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
            var winchFixture = Asset<ShipFixtureDefinition>(WinchFixturePath, fixture =>
            {
                fixture.Prefab = WinchPrefab(crank, wood, iron);
                fixture.LocalPosition = WinchLocalPosition;
                fixture.LocalEulerAngles = WinchLocalEuler;
            });

            // The same winch as a socket fitting: designed ships mount it where their design has room (the Galleon's balcony).
            var winchFitting = Asset<ShipFittingDefinition>(WinchFittingPath, fitting =>
            {
                var fields = new SerializedObject(fitting);
                fields.FindProperty("_fittingId").stringValue = WinchFittingId;
                fields.FindProperty("_displayName").stringValue = "Leash winch";
                fields.FindProperty("_prefab").objectReferenceValue = winchFixture.Prefab;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            // The Leashed input context (Interact: the respawn choice) needs the tag above; the input builder makes it.
            AssetDatabase.SaveAssets();
            InputAssetBuilder.Build();
            InputMapWriter.Write();
            var leashedControls = Load<InputContext>(LeashedContextPath);

            var installer = Asset<RespawnFeatureInstaller>(InstallerPath, i =>
            {
                SetField(i, "_rules", rules);
                SetField(i, "_stateFixture", stateFixture);
                SetField(i, "_leashFixture", winchFixture);
                SetField(i, "_leashedControls", leashedControls);
                var fittings = new SerializedObject(i);
                var list = fittings.FindProperty("_fittings");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = winchFitting;
                fittings.ApplyModifiedPropertiesWithoutUndo();
            });

            AddUnique(Load<FeatureProfile>(MainProfilePath), "_installers", installer);
            var testProfile = Asset<FeatureProfile>(TestProfilePath, _ => { });
            AddUnique(testProfile, "_includes", Load<FeatureProfile>(TestCoreProfilePath));
            // The hook gives the scenario's subject something to carry, and to lose.
            AddUnique(testProfile, "_installers", installer, Load<FeatureInstaller>(GrappleHookInstallerPath));

            AssetDatabase.SaveAssets();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenes.Respawn) == null) TestRangeSceneBuilder.BuildArea(TestScenes.Respawn);
            Debug.Log("[RespawnAssetBuilder] Respawn assets are up to date.");
        }

        /// <summary>
        /// The leash winch fixture: a leash point (the rope's Anchor past the rail, the Stand spot on deck, facing inboard)
        /// with a winch on it, an Interact target (IA_CrankWinch). Its root is on the deck facing out (+z), so the anchor
        /// is 0.9 m outward at rail height.
        /// </summary>
        private static GameObject WinchPrefab(InteractionDefinition crank, Material wood, Material iron)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(WinchPrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("LeashWinch");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            root.AddComponent<LeashPointNetworkMediator>();
            var winch = root.AddComponent<LeashWinchNetworkMediator>();
            var fields = new SerializedObject(winch);
            fields.FindProperty("_interactionDefinition").objectReferenceValue = crank;
            fields.ApplyModifiedPropertiesWithoutUndo();

            Child("Anchor", root.transform, new Vector3(0f, 1f, 0.9f), Vector3.zero);
            Child("Stand", root.transform, new Vector3(0f, 1.1f, -1.5f), new Vector3(0f, 180f, 0f));

            Visual(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(0.9f, 0.3f, 0.6f), wood);
            Visual(PrimitiveType.Cube, "Post_L", root.transform, new Vector3(-0.4f, 0.6f, 0f), Vector3.zero, new Vector3(0.1f, 0.9f, 0.1f), wood);
            Visual(PrimitiveType.Cube, "Post_R", root.transform, new Vector3(0.4f, 0.6f, 0f), Vector3.zero, new Vector3(0.1f, 0.9f, 0.1f), wood);
            Visual(PrimitiveType.Cylinder, "Drum", root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.35f, 0.35f, 0.35f), iron);
            Visual(PrimitiveType.Cube, "Crank", root.transform, new Vector3(0.5f, 0.9f, 0.15f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.35f), iron);
            Visual(PrimitiveType.Cube, "Arm", root.transform, new Vector3(0f, 1f, 0.45f), Vector3.zero, new Vector3(0.08f, 0.08f, 0.9f), iron);

            var volume = new GameObject("InteractVolume").AddComponent<BoxCollider>();
            volume.transform.SetParent(root.transform, false);
            volume.transform.localPosition = new Vector3(0f, 1f, 0f);
            volume.isTrigger = true;
            volume.size = new Vector3(1.2f, 2f, 1.2f);

            EnsureFolder(WinchPrefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, WinchPrefabPath);
            Object.DestroyImmediate(root);
            Revalidate(prefab);
            return prefab;
        }

        private static void Child(string name, Transform parent, Vector3 position, Vector3 euler)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            child.localEulerAngles = euler;
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

        // The NetworkObject's id was hashed while it was a scene object; its validation re-hashes it from the asset, or a
        // client would refuse the host.
        private static void Revalidate(GameObject prefab)
        {
            var saved = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(saved, null);
            EditorUtility.SetDirty(saved);
            EditorUtility.SetDirty(prefab);
        }

        /// <summary>The RespawnState fixture: a NetworkObject carrying a player's respawn choice to the server.</summary>
        private static GameObject StatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatePrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("RespawnState");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            root.AddComponent<RespawnStateNetworkMediator>();

            EnsureFolder(StatePrefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StatePrefabPath);
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
