#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Gas;
using TinCan.Core.Interaction;
using TinCan.Core.Ship.Sockets;
using TinCan.DevTools.Scenarios;
using TinCan.Features.Navigation;
using TinCan.Features.Stations;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Navigation > Build Assets builds every asset of navigation, so its data is reviewable as code:
    /// <list type="bullet">
    /// <item>the config and its materials;</item>
    /// <item>the compass prefab shown at every helm (a glass sphere, a disc fixed to the ship, a 3D arrow, a level water line);</item>
    /// <item>the NavigationState prefab (the replicated route) and its ship fixture;</item>
    /// <item>the installer, its place in Profile_FuelSandbox (and so the Galleon's profile) and Profile_Test_Navigation, and that area's scene.</item>
    /// </list>
    /// Scriptable objects are re-applied on every run. Prefabs are built only when missing (delete one to rebuild it), so
    /// network ids stay stable. Plan: .docs/plans/navigation-compass.md.
    /// </summary>
    public static class NavigationAssetBuilder
    {
        private const string ConfigPath = "Assets/Settings/Navigation/NavigationConfig.asset";
        private const string CompassPrefabPath = "Assets/Prefabs/Navigation/Compass.prefab";
        private const string StatePrefabPath = "Assets/Prefabs/Navigation/NavigationState.prefab";
        private const string StateFixturePath = "Assets/Settings/Fixtures/NavigationStateFixture.asset";
        private const string InstallerPath = "Assets/Resources/Installers/NavigationFeatureInstaller.asset";
        private const string Installers = "Assets/Resources/Installers/";
        private const string MainProfilePath = "Assets/Settings/FeatureProfiles/Profile_FuelSandbox.asset";
        private const string TestProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Navigation.asset";
        private const string TestCoreProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Core.asset";
        private const string Materials = "Assets/Materials/Navigation/";
        private const string Tags = "Assets/Abilities/Tags/";
        private const string TakeTablePath = "Assets/Interactions/IA_TakeChartTable.asset";
        private const string TablePrefabPath = "Assets/Prefabs/Navigation/ChartTable.prefab";
        private const string TableFixturePath = "Assets/Settings/Fixtures/ChartTableFixture.asset";
        private const string TableFittingPath = "Assets/Settings/ShipFittings/FITTING_ChartTable.asset";
        private const string NavigatorContextPath = "Assets/Input/Contexts/Context_Navigator.asset";
        public const string ChartTableFittingId = "navigation.chart_table";

        // The table's paper and the chart on it (table metres).
        private const float ChartRadius = 0.4f;

        // On the mid deck (top y -3.49 on the airship and the test ship), to port and a little aft of amidships, inboard
        // of the port rail (the test ship's runs at x -4.2), the navigator standing on its aft side facing the bow.
        // Please check this spot against the airship's props in the Inspector.
        private static readonly Vector3 TableLocalPosition = new(-2.8f, -3.49f, -2f);

        // The navigator's view: over the table's aft edge, looking down at the chart without seeing their own body.
        private static readonly Vector3 ViewCameraPosition = new(0f, 1.95f, -0.3f);
        private static readonly Vector3 ViewCameraEuler = new(65f, 0f, 0f);

        [MenuItem("TinCan/Dev/Navigation/Build Assets")]
        public static void Build()
        {
            var brass = Material("M_Compass_Brass", new Color(0.78f, 0.6f, 0.25f));
            var glass = Material("M_Compass_Glass", new Color(0.8f, 0.92f, 1f, 0.18f), transparent: true);
            var water = Material("M_Compass_Water", new Color(0.2f, 0.55f, 0.9f, 0.45f), transparent: true);
            var needle = Material("M_Compass_Needle", new Color(0.9f, 0.15f, 0.1f), unlit: true);
            var tail = Material("M_Compass_Tail", new Color(0.25f, 0.25f, 0.28f));
            var bow = Material("M_Compass_Bow", new Color(0.95f, 0.95f, 0.85f), unlit: true);

            var wood = Material("M_ChartTable_Wood", new Color(0.42f, 0.28f, 0.16f));
            var paper = Material("M_Chart_Paper", new Color(0.93f, 0.87f, 0.7f));

            var config = Asset<NavigationConfig>(ConfigPath, c =>
            {
                c.CompassPrefab = CompassPrefab(brass, glass, water, needle, tail, bow);
                // The chart fills the table's paper; the two go together.
                c.ChartRadius = ChartRadius;
                c.ShipMarkMaterial = Material("M_Chart_Ship", new Color(0.12f, 0.2f, 0.45f), unlit: true);
                c.RouteMaterial = Material("M_Chart_Route", new Color(0.55f, 0.12f, 0.08f), unlit: true);
                c.ActiveLegMaterial = Material("M_Chart_ActiveLeg", new Color(0.95f, 0.2f, 0.1f), unlit: true);
                c.PoiMaterial = Material("M_Chart_Poi", new Color(0.95f, 0.75f, 0.1f), unlit: true);
                c.RockMaterial = Material("M_Chart_Rock", new Color(0.45f, 0.4f, 0.33f));
                c.HazardMaterial = Material("M_Chart_Hazard", new Color(0.9f, 0.15f, 0.1f, 0.4f), transparent: true);
                c.CursorMaterial = Material("M_Chart_Cursor", new Color(0.05f, 0.05f, 0.05f), unlit: true);
            });

            var stateFixture = Asset<ShipFixtureDefinition>(StateFixturePath, fixture =>
            {
                fixture.Prefab = StatePrefab();
                fixture.LocalPosition = Vector3.zero;
                fixture.LocalEulerAngles = Vector3.zero;
            });

            // The chart table: a station (the occupy ability holds State.Occupying.Chart, which turns Context_Navigator on).
            var occupying = Load<GameplayTag>(Tags + "State.Occupying.asset");
            var occupyingChart = Tag("State.Occupying.Chart", occupying);
            var occupyTag = Tag("Ability.Occupy.Chart");
            Load<GameplayTagDatabase>("Assets/Abilities/GameplayTagDatabase.asset").Refresh();
            var moveSpeed = Load<GameplayAttribute>("Assets/Abilities/Attributes/Attr_MoveSpeed.asset");
            var jumpForce = Load<GameplayAttribute>("Assets/Abilities/Attributes/Attr_JumpForce.asset");
            var occupyingEffect = Asset<GameplayEffectDefinition>("Assets/Abilities/Effects/GE_Occupying_Chart.asset", effect =>
            {
                effect.DurationType = DurationType.Infinite;
                effect.GrantedTags = new List<GameplayTag> { occupyingChart };
                effect.Modifiers = new List<AttributeModifier>
                {
                    new() { Attribute = moveSpeed, Operation = ModifierOp.Override, Value = 0f },
                    new() { Attribute = jumpForce, Operation = ModifierOp.Override, Value = 0f }
                };
                effect.Cues ??= new List<GameplayTag>();
            });
            var occupyAbility = Asset<AbilityDefinition>("Assets/Abilities/AbilityDefinitions/GA_OccupyChartTable.asset", ability =>
            {
                ResetAbility(ability);
                ability.AbilityTag = occupyTag;
                ability.ActiveEffect = occupyingEffect;
                ability.ActiveEffectTarget = EffectTarget.Self;
                ability.ActivationBlockedTagsOnActor.Add(occupying);
            });
            var takeTable = Asset<InteractionDefinition>(TakeTablePath, definition =>
            {
                var fields = new SerializedObject(definition);
                fields.FindProperty("_handlerTypeName").stringValue = typeof(OccupyStationInteractionHandler).AssemblyQualifiedName!;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
            var tableFixture = Asset<ShipFixtureDefinition>(TableFixturePath, fixture =>
            {
                fixture.Prefab = ChartTablePrefab(takeTable, occupyAbility, wood, paper);
                PlaceViewCamera(TablePrefabPath);
                fixture.LocalPosition = TableLocalPosition;
                fixture.LocalEulerAngles = Vector3.zero;
            });
            var tableFitting = Asset<ShipFittingDefinition>(TableFittingPath, fitting =>
            {
                var fields = new SerializedObject(fitting);
                fields.FindProperty("_fittingId").stringValue = ChartTableFittingId;
                fields.FindProperty("_displayName").stringValue = "Chart table";
                fields.FindProperty("_prefab").objectReferenceValue = tableFixture.Prefab;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            // Context_Navigator needs the tag above; the input builder makes it. INPUT_MAP.md is written once the installer
            // points at it (the map names the installer that contributes each context).
            AssetDatabase.SaveAssets();
            InputAssetBuilder.Build();
            var controls = Load<NavigatorInputContext>(NavigatorContextPath);

            var installer = Asset<NavigationFeatureInstaller>(InstallerPath, i =>
            {
                SetField(i, "_config", config);
                SetField(i, "_stateFixture", stateFixture);
                SetField(i, "_tableFixture", tableFixture);
                SetField(i, "_controls", controls);
                var fields = new SerializedObject(i);
                var list = fields.FindProperty("_fittings");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = tableFitting;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            AddUnique(Load<FeatureProfile>(MainProfilePath), "_installers", installer);

            // The test area: the core test range, a helm (with Stations), a voyage for the destination, sky islands and
            // hazard zones to chart, and navigation.
            var testProfile = Asset<FeatureProfile>(TestProfilePath, _ => { });
            AddUnique(testProfile, "_includes", Load<FeatureProfile>(TestCoreProfilePath));
            AddUnique(testProfile, "_installers",
                Load<Object>(Installers + "StationsFeatureInstaller.asset"),
                Load<Object>(Installers + "HelmFeatureInstaller.asset"),
                Load<Object>(Installers + "VoyageFeatureInstaller.asset"),
                Load<Object>(Installers + "SkyIslandsFeatureInstaller.asset"),
                Load<Object>(Installers + "SkyHazardsFeatureInstaller.asset"),
                installer);

            AssetDatabase.SaveAssets();
            InputMapWriter.Write();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenes.Navigation) == null) TestRangeSceneBuilder.BuildArea(TestScenes.Navigation);
            Debug.Log("[NavigationAssetBuilder] Navigation assets are up to date.");
        }

        /// <summary>
        /// The compass, about 0.4 m across, on a post down to the helm stand. Its +z is the bow. Children the view looks up
        /// by name: Arrow (pivot at the centre, +z points) and WaterLine (kept level). No colliders: it never blocks Interact.
        /// </summary>
        private static GameObject CompassPrefab(Material brass, Material glass, Material water, Material needle, Material tail, Material bow)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CompassPrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("Compass");
            root.AddComponent<CompassView>();

            Visual(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, -0.55f, 0f), Vector3.zero, new Vector3(0.06f, 0.45f, 0.06f), brass);
            Visual(PrimitiveType.Cylinder, "Disc", root.transform, Vector3.zero, Vector3.zero, new Vector3(0.3f, 0.004f, 0.3f), brass);
            Visual(PrimitiveType.Cube, "BowMark", root.transform, new Vector3(0f, 0.004f, 0.135f), new Vector3(0f, 45f, 0f), new Vector3(0.025f, 0.006f, 0.025f), bow);
            Visual(PrimitiveType.Cylinder, "Gimbal", root.transform, new Vector3(0f, -0.2f, 0f), Vector3.zero, new Vector3(0.12f, 0.02f, 0.12f), brass);

            var waterLine = new GameObject("WaterLine").transform;
            waterLine.SetParent(root.transform, false);
            Visual(PrimitiveType.Cylinder, "Surface", waterLine, Vector3.zero, Vector3.zero, new Vector3(0.34f, 0.0015f, 0.34f), water, shadows: false);

            var arrow = new GameObject("Arrow").transform;
            arrow.SetParent(root.transform, false);
            Visual(PrimitiveType.Cube, "Shaft", arrow, new Vector3(0f, 0f, 0.01f), Vector3.zero, new Vector3(0.014f, 0.014f, 0.22f), needle);
            Visual(PrimitiveType.Cube, "Tip", arrow, new Vector3(0f, 0f, 0.12f), new Vector3(0f, 45f, 0f), new Vector3(0.04f, 0.014f, 0.04f), needle);
            Visual(PrimitiveType.Cube, "Tail", arrow, new Vector3(0f, 0f, -0.1f), Vector3.zero, new Vector3(0.03f, 0.03f, 0.03f), tail);

            Visual(PrimitiveType.Sphere, "Glass", root.transform, Vector3.zero, Vector3.zero, new Vector3(0.38f, 0.38f, 0.38f), glass, shadows: false);

            EnsureFolder(CompassPrefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CompassPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// The chart table, 1 m by 0.9 m (it fits a mount plate): a station (IA_TakeChartTable, GA_OccupyChartTable) with the
        /// navigator's Seat on its aft side, the Chart's centre on the paper (scale 1, so chart marks are in table metres),
        /// and a disabled ViewCamera (and ears) looking down at the chart from the navigator's head. Its root is on the deck,
        /// +z toward the bow.
        /// </summary>
        private static GameObject ChartTablePrefab(InteractionDefinition take, AbilityDefinition occupy, Material wood, Material paper)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabPath);
            if (existing != null) return existing;

            const float topHeight = 0.9f;
            var root = new GameObject("ChartTable");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            var mediator = root.AddComponent<ChartTableNetworkMediator>();
            var fields = new SerializedObject(mediator);
            fields.FindProperty("_interactionDefinition").objectReferenceValue = take;
            fields.FindProperty("_occupyAbility").objectReferenceValue = occupy;
            fields.ApplyModifiedPropertiesWithoutUndo();

            // The top is solid (and what Interact sees); the legs are not.
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Top";
            top.transform.SetParent(root.transform, false);
            top.transform.localPosition = new Vector3(0f, topHeight - 0.025f, 0f);
            top.transform.localScale = new Vector3(1f, 0.05f, 0.9f);
            top.GetComponent<MeshRenderer>().sharedMaterial = wood;
            foreach (float x in new[] { -0.43f, 0.43f })
            foreach (float z in new[] { -0.38f, 0.38f })
                Visual(PrimitiveType.Cube, "Leg", root.transform, new Vector3(x, (topHeight - 0.05f) * 0.5f, z), Vector3.zero, new Vector3(0.07f, topHeight - 0.05f, 0.07f), wood);

            var chart = new GameObject("Chart").transform;
            chart.SetParent(root.transform, false);
            chart.localPosition = new Vector3(0f, topHeight + 0.001f, 0f);
            Visual(PrimitiveType.Cylinder, "Paper", chart, Vector3.zero, Vector3.zero, new Vector3(ChartRadius * 2.08f, 0.001f, ChartRadius * 2.08f), paper, shadows: false);

            Child("Seat", root.transform, new Vector3(0f, 0f, -0.85f), Vector3.zero);

            var view = Child("ViewCamera", root.transform, ViewCameraPosition, ViewCameraEuler);
            var camera = view.gameObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 60f;
            view.gameObject.AddComponent<AudioListener>().enabled = false;

            var volume = new GameObject("InteractVolume").AddComponent<BoxCollider>();
            volume.transform.SetParent(root.transform, false);
            volume.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            volume.isTrigger = true;
            volume.size = new Vector3(1.1f, 1.2f, 1f);

            EnsureFolder(TablePrefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TablePrefabPath);
            Object.DestroyImmediate(root);
            Revalidate(prefab);
            return prefab;
        }

        /// <summary>Puts the table's ViewCamera at its pose in the existing prefab (in place, so the network id is kept).</summary>
        private static void PlaceViewCamera(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.transform.Find("ViewCamera");
                if (view == null || (view.localPosition == ViewCameraPosition && view.localEulerAngles == ViewCameraEuler)) return;
                view.localPosition = ViewCameraPosition;
                view.localEulerAngles = ViewCameraEuler;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform Child(string name, Transform parent, Vector3 position, Vector3 euler)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            child.localEulerAngles = euler;
            return child;
        }

        private static GameplayTag Tag(string name, GameplayTag? parent = null) =>
            Asset<GameplayTag>(Tags + name + ".asset", tag =>
            {
                var fields = new SerializedObject(tag);
                fields.FindProperty("_parent").objectReferenceValue = parent;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

        private static void ResetAbility(AbilityDefinition ability)
        {
            ability.CancelAbilitiesWithTag = new List<GameplayTag>();
            ability.BlockAbilitiesWithTag = new List<GameplayTag>();
            ability.ActivationRequiredTagsOnActor = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnActor = new List<GameplayTag>();
            ability.ActivationRequiredTagsOnTarget = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnTarget = new List<GameplayTag>();
            ability.TimingTagWindows = new List<AbilityTagWindow>();
            ability.TriggerInput = null!;
        }

        /// <summary>The NavigationState fixture: a NetworkObject carrying the ship's route.</summary>
        private static GameObject StatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatePrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("NavigationState");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            root.AddComponent<NavigationNetworkMediator>();

            EnsureFolder(StatePrefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StatePrefabPath);
            Object.DestroyImmediate(root);
            Revalidate(prefab);
            return prefab;
        }

        private static void Visual(PrimitiveType shape, string name, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material, bool shadows = true)
        {
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = name;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localEulerAngles = euler;
            visual.transform.localScale = scale;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (!shadows) renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Material Material(string name, Color color, bool unlit = false, bool transparent = false)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")
                         ?? throw new InvalidOperationException("URP shader not found.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            if (transparent) MakeTransparent(material);
            EnsureFolder(path);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // URP's surface type, set the way its material inspector sets it.
        private static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
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

        private static void SetField(Object target, string field, Object? value)
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
