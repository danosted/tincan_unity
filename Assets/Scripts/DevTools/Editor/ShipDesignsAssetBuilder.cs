#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Ship.Parts;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.Helm;
using TinCan.Features.ShipDesigns;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Ship Designs > Build Assets: the ship parts that hang on a frame (the helm as the core part,
    /// contributed by the Helm installer; the cairn, the engine, the propeller, the mast, the mount plate), their greybox
    /// prefabs and materials, the ship designs config and installer, and the built-in designs (<see cref="ShipFrameDesigns"/>,
    /// written through the codec, so the files are in canonical form). Materials are created only when missing, so their
    /// colours stay as tuned. Every prefab's pivot is its anchor point: on the floor for a face part, at the node for a
    /// node part. Plans: .docs/plans/modular-airship-builder.md, .docs/plans/frame-only-ships.md.
    /// </summary>
    public static class ShipDesignsAssetBuilder
    {
        public const string PartsFolder = "Assets/Settings/ShipParts/";
        public const string DesignsFolder = "Assets/Settings/ShipDesigns/";
        public const string ConfigPath = DesignsFolder + "ShipDesignsConfig.asset";
        public const string StarterPath = DesignsFolder + "Starter.ship.json";
        public const string GalleonPath = DesignsFolder + "Galleon.ship.json";
        public const string RaftPath = DesignsFolder + "Raft.ship.json";
        public const string InstallerPath = "Assets/Resources/Installers/ShipDesignsFeatureInstaller.asset";
        private const string PrefabsFolder = "Assets/Prefabs/ShipParts/";
        private const string MaterialsFolder = "Assets/Materials/ShipParts/";
        private const string HelmInstallerPath = "Assets/Resources/Installers/HelmFeatureInstaller.asset";
        private const string HelmStationPath = "Assets/Prefabs/Airship/Parts/HelmStation.prefab";
        private const string StationsInstallerPath = "Assets/Resources/Installers/StationsFeatureInstaller.asset";
        private const string StatePrefabPath = "Assets/Prefabs/ShipDesigns/ShipDesignState.prefab";
        private const string StateFixturePath = "Assets/Settings/Fixtures/ShipDesignStateFixture.asset";
        private const string TestShipPath = "Assets/Prefabs/Test/TestShip_Prefab.prefab";
        public const string ModularShipPath = "Assets/Prefabs/Airship/ModularShip_Prefab.prefab";
        public const string ProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Shipyard.asset";
        private const string CoreProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Core.asset";

        public const string CoreHelm = "core.helm";
        public const string Cairn = "spirit.cairn";
        public const string Engine = "prop.engine";
        public const string Propeller = "prop.propeller";
        public const string Mast = "hull.mast";
        public const string HullMount = "hull.mount";

        /// <summary>A mount plate's thickness; it lies on the floor, its socket on top.</summary>
        private const float PlateThickness = 0.1f;

        [MenuItem("TinCan/Dev/Ship Designs/Build Assets")]
        public static void Build()
        {
            var wood = Material("M_ShipHull", new Color(0.45f, 0.30f, 0.18f));
            var brass = Material("M_ShipEngine", new Color(0.55f, 0.42f, 0.2f));
            var stone = Material("M_ShipCairn", new Color(0.42f, 0.44f, 0.47f));
            var spirit = Material("M_ShipSpirit", new Color(0.55f, 0.85f, 1f));

            // The engine fills its block and the one behind it (-Z).
            var enginePrefab = BoxPrefab("ShipPart_Engine", new Vector3(0f, 0.5f, -0.5f), new Vector3(0.9f, 0.9f, 2f), brass);
            var engine = Part("PART_Engine", Engine, "Engine", "Drive", ShipPartAttach.Face, enginePrefab, null, Vector3.zero,
                new ShipPartStats(40f, thrust: 200f, hull: 40f), new Vector3Int(0, 0, 0), new Vector3Int(0, 0, -1));
            // A nacelle on its node and a 3 x 3 propeller disc one block behind it (-Z).
            var propeller = Part("PART_Propeller", Propeller, "Propeller", "Drive", ShipPartAttach.Node, PropellerPrefab(brass, wood), null,
                new Vector3(0f, 0.5f, 0f), new ShipPartStats(25f, thrust: 400f, hull: 20f),
                new[] { Vector3Int.zero }.Concat(from x in Enumerable.Range(-1, 3) from y in Enumerable.Range(-1, 3) select new Vector3Int(x, y, -1)).ToArray());
            // A mast 4 m tall standing on its node: for sails, later.
            var mastPrefab = BoxPrefab("ShipPart_Mast", new Vector3(0f, 2f, 0f), new Vector3(0.25f, 2f, 0.25f), wood, PrimitiveType.Cylinder);
            var mast = Part("PART_Mast", Mast, "Mast", "Hull", ShipPartAttach.Node, mastPrefab, null, Vector3.zero, new ShipPartStats(10f, hull: 15f),
                Enumerable.Range(0, 4).Select(y => new Vector3Int(0, y, 0)).ToArray());
            // A plate on the floor with a socket on top: where fittings (a cannon, a tool rack) are mounted.
            var mount = Part("PART_HullMount", HullMount, "Mount plate", "Hull", ShipPartAttach.Face, MountPrefab(brass), null, Vector3.zero,
                new ShipPartStats(5f, hull: 10f));
            // The cairn: a stone holding the ship's spirit, which lifts the ship (ShipStatsTuning.SpiritLift).
            var cairn = Part("PART_Cairn", Cairn, "Cairn", "Spirit", ShipPartAttach.Face, CairnPrefab(stone, spirit), null, Vector3.zero,
                new ShipPartStats(40f, hull: 40f), new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 0));
            SetFlag(cairn, "_isCairn");
            // The helm stand is about 4.5 x 2.2 x 3.9 m with its pivot at its base, reaching 2.35 m forward: 3 x 2 x 5 blocks.
            var helm = Part("PART_CoreHelm", CoreHelm, "Helm", "Core", ShipPartAttach.Face, null, Load<GameObject>(HelmStationPath), Vector3.zero,
                new ShipPartStats(30f, hull: 100f), Box(3, 2, 5));
            SetFlag(helm, "_isCore");

            var starter = WriteDesign(StarterPath, ShipFrameDesigns.StarterDesign());
            var galleon = WriteDesign(GalleonPath, ShipFrameDesigns.GalleonDesign());
            var raft = WriteDesign(RaftPath, ShipFrameDesigns.RaftDesign());
            var config = Asset<ShipDesignsConfig>(ConfigPath, designs =>
            {
                designs.BuiltInDesigns = new List<TextAsset> { starter, galleon, raft };
                designs.DefaultDesign = ShipFrameDesigns.StarterName;
                Configure(designs);
            });

            var stateFixture = Asset<ShipFixtureDefinition>(StateFixturePath, fixture =>
            {
                fixture.Prefab = StatePrefab();
                fixture.LocalPosition = Vector3.zero;
                fixture.LocalEulerAngles = Vector3.zero;
            });
            var installer = Asset<ShipDesignsFeatureInstaller>(InstallerPath, i =>
            {
                SetField(i, "_config", config);
                SetList(i, "_parts", new Object[] { cairn, engine, propeller, mast, mount });
                SetField(i, "_stateFixture", stateFixture);
            });
            var helmInstaller = Load<HelmFeatureInstaller>(HelmInstallerPath);
            SetList(helmInstaller, "_parts", new Object[] { helm });

            ModularShipPrefab();
            Asset<FeatureProfile>(ProfilePath, profile =>
            {
                SetList(profile, "_includes", new Object[] { Load<FeatureProfile>(CoreProfilePath) });
                // Added, never replaced: other builders (the shipyard) add their installers to this profile too.
                AddUnique(profile, "_installers", Load<FeatureInstaller>(StationsInstallerPath), helmInstaller, installer);
            });

            AssetDatabase.SaveAssets();
            Debug.Log($"[ShipDesignsAssetBuilder] Ship design assets built ({installer.name}; {ShipFrameDesigns.StarterDesign()}, "
                      + $"{ShipFrameDesigns.GalleonDesign()}, {ShipFrameDesigns.RaftDesign()}).");
        }

        /// <summary>The config's frame materials, and defaults for limits and tuning added after the asset was made.</summary>
        private static void Configure(ShipDesignsConfig designs)
        {
            designs.FramePlanks = Material("M_ShipDeck", new Color(0.66f, 0.52f, 0.34f));
            designs.FrameBeams = Material("M_ShipHull", new Color(0.45f, 0.30f, 0.18f));

            var limits = designs.Limits;
            var defaultLimits = ShipDesignLimits.Default;
            limits.MaxFrameElements = defaultLimits.MaxFrameElements;
            limits.MaxStories = defaultLimits.MaxStories;
            limits.MaxFaceNodes = defaultLimits.MaxFaceNodes;
            designs.Limits = limits;

            var frames = designs.Frames;
            var defaultFrames = ShipFrameTuning.Default;
            if (frames.MinHeadroom <= 0f) frames.MinHeadroom = defaultFrames.MinHeadroom;
            if (frames.RailHeight <= 0f) frames.RailHeight = defaultFrames.RailHeight;
            if (frames.StairSlope <= 0f) frames.StairSlope = defaultFrames.StairSlope;
            if (frames.StepRise <= 0f) frames.StepRise = defaultFrames.StepRise;
            designs.Frames = frames;

            var flight = designs.Flight;
            if (flight.SpiritLift <= 0f) flight.SpiritLift = ShipStatsTuning.Default.SpiritLift;
            designs.Flight = flight;
        }

        /// <summary>The ShipDesignState fixture: a NetworkObject carrying a ship's design, riding the ship as a child.</summary>
        private static GameObject StatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatePrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("ShipDesignState");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            root.AddComponent<ShipDesignNetworkMediator>();

            EnsureFolder(StatePrefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StatePrefabPath);
            Object.DestroyImmediate(root);
            Rehash(prefab);
            return prefab;
        }

        /// <summary>
        /// The ship root designed ships are built on: the test ship (movement, health, abilities, entity, on-board volume)
        /// without its deck geometry. Its volume starts small; the assembler fits it to the design.
        /// </summary>
        private static void ModularShipPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModularShipPath) != null) return;
            if (!AssetDatabase.CopyAsset(TestShipPath, ModularShipPath)) throw new InvalidOperationException($"Could not copy {TestShipPath}.");

            var root = PrefabUtility.LoadPrefabContents(ModularShipPath);
            try
            {
                var geometry = root.transform.Find("TestRangeGeometry");
                if (geometry != null) Object.DestroyImmediate(geometry.gameObject);
                var volume = root.GetComponent<BoxCollider>();
                volume.center = Vector3.zero;
                volume.size = new Vector3(4f, 4f, 4f);
                PrefabUtility.SaveAsPrefabAsset(root, ModularShipPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Rehash(AssetDatabase.LoadAssetAtPath<GameObject>(ModularShipPath));
        }

        /// <summary>
        /// A NetworkObject's id is hashed from its asset; a new or copied prefab keeps a stale one until validated. Without
        /// this a client refuses the host ("NetworkConfig mismatch").
        /// </summary>
        private static void Rehash(GameObject prefab)
        {
            var networkObject = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            EditorUtility.SetDirty(prefab);
        }

        private static TextAsset WriteDesign(string path, ShipDesign design)
        {
            EnsureFolder(path);
            var text = new ShipDesignJsonCodec(ShipDesignLimits.Default).Encode(design);
            File.WriteAllText(Path.GetFullPath(path), text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return Load<TextAsset>(path);
        }

        private static Vector3Int[] Box(int width, int height, int depth) =>
            (from x in Enumerable.Range(-(width / 2), width) from y in Enumerable.Range(0, height) from z in Enumerable.Range(-(depth / 2), depth)
                select new Vector3Int(x, y, z)).ToArray();


        /// <summary>A stone pillar on the floor with the spirit, a glowing ball, above it.</summary>
        private static GameObject CairnPrefab(Material stone, Material spirit)
        {
            var root = new GameObject("ShipPart_Cairn");
            Piece(root, "Stone", PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(0.8f, 1.2f, 0.8f), stone, solid: true);
            Piece(root, "Spirit", PrimitiveType.Sphere, new Vector3(0f, 1.55f, 0f), Vector3.one * 0.45f, spirit, solid: false);
            return Save(root);
        }

        private static void SetFlag(ShipPartDefinition part, string field)
        {
            var serialized = new SerializedObject(part);
            serialized.FindProperty(field).boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A nacelle along Z in its cell and four blades in the cell layer behind it; only the nacelle is solid.</summary>
        private static GameObject PropellerPrefab(Material metal, Material blades)
        {
            var root = new GameObject("ShipPart_Propeller");
            Piece(root, "Nacelle", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.6f, 0.5f, 0.6f), metal, solid: true, Quaternion.Euler(90f, 0f, 0f));
            Piece(root, "Hub", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.6f), Vector3.one * 0.35f, metal, solid: false);
            foreach (float angle in new[] { 45f, 135f })
            {
                Piece(root, "Blades", PrimitiveType.Cube, new Vector3(0f, 0f, -1f), new Vector3(2.8f, 0.22f, 0.06f), blades, solid: false,
                    Quaternion.Euler(0f, 0f, angle));
            }

            return Save(root);
        }

        private static void Piece(GameObject root, string name, PrimitiveType shape, Vector3 centre, Vector3 size, Material material, bool solid,
            Quaternion? rotation = null)
        {
            var piece = GameObject.CreatePrimitive(shape);
            piece.name = name;
            piece.transform.SetParent(root.transform, false);
            piece.transform.localPosition = centre;
            piece.transform.localRotation = rotation ?? Quaternion.identity;
            piece.transform.localScale = size;
            piece.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(piece.GetComponent<Collider>());
        }

        private static GameObject Save(GameObject root)
        {
            var path = PrefabsFolder + root.name + ".prefab";
            try
            {
                EnsureFolder(path);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A part asset, written whole; <see cref="SetFlag"/> marks the core and the cairn afterwards.</summary>
        private static ShipPartDefinition Part(string asset, string partId, string displayName, string category, ShipPartAttach attach,
            GameObject? visual, GameObject? networkedPrefab, Vector3 pivotOffset, ShipPartStats stats, params Vector3Int[] footprint) =>
            Asset<ShipPartDefinition>(PartsFolder + asset + ".asset", part =>
            {
                var serialized = new SerializedObject(part);
                serialized.FindProperty("_partId").stringValue = partId;
                serialized.FindProperty("_displayName").stringValue = displayName;
                serialized.FindProperty("_category").stringValue = category;
                serialized.FindProperty("_attach").enumValueIndex = (int)attach;
                serialized.FindProperty("_isCore").boolValue = false;
                serialized.FindProperty("_isCairn").boolValue = false;
                serialized.FindProperty("_visual").objectReferenceValue = visual;
                serialized.FindProperty("_networkedPrefab").objectReferenceValue = networkedPrefab;
                serialized.FindProperty("_pivotOffset").vector3Value = pivotOffset;
                var statsProperty = serialized.FindProperty("_stats");
                statsProperty.FindPropertyRelative(nameof(ShipPartStats.Mass)).floatValue = stats.Mass;
                statsProperty.FindPropertyRelative(nameof(ShipPartStats.Lift)).floatValue = stats.Lift;
                statsProperty.FindPropertyRelative(nameof(ShipPartStats.Thrust)).floatValue = stats.Thrust;
                statsProperty.FindPropertyRelative(nameof(ShipPartStats.Hull)).floatValue = stats.Hull;
                var cells = serialized.FindProperty("_footprint");
                cells.arraySize = footprint.Length;
                for (int i = 0; i < footprint.Length; i++) cells.GetArrayElementAtIndex(i).vector3IntValue = footprint[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

        /// <summary>A plate lying on the floor in a mount colour, with a ShipSocket on top.</summary>
        private static GameObject MountPrefab(Material material)
        {
            var path = PrefabsFolder + "ShipPart_HullMount.prefab";
            var root = new GameObject("ShipPart_HullMount");
            try
            {
                var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "Mesh";
                plate.transform.SetParent(root.transform, false);
                plate.transform.localPosition = new Vector3(0f, PlateThickness * 0.5f, 0f);
                plate.transform.localScale = new Vector3(1f, PlateThickness, 1f);
                plate.GetComponent<MeshRenderer>().sharedMaterial = material;
                var socket = new GameObject("ShipSocket");
                socket.transform.SetParent(root.transform, false);
                socket.transform.localPosition = new Vector3(0f, PlateThickness, 0f);
                socket.AddComponent<ShipSocket>();
                EnsureFolder(path);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BoxPrefab(string name, Vector3 centre, Vector3 size, Material material, PrimitiveType shape = PrimitiveType.Cube)
        {
            var path = PrefabsFolder + name + ".prefab";
            var box = GameObject.CreatePrimitive(shape);
            try
            {
                box.name = name;
                var mesh = box.transform;
                var root = new GameObject(name);
                mesh.SetParent(root.transform, false);
                mesh.name = "Mesh";
                mesh.localPosition = centre;
                mesh.localScale = size;
                box.GetComponent<MeshRenderer>().sharedMaterial = material;
                if (shape != PrimitiveType.Cube)
                {
                    // A box to aim at and stand on: a scaled sphere collider would be a ball of the largest radius.
                    Object.DestroyImmediate(box.GetComponent<Collider>());
                    box.AddComponent<BoxCollider>();
                }

                EnsureFolder(path);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                return prefab;
            }
            finally
            {
                if (box != null) Object.DestroyImmediate(box);
            }
        }

        private static Material Material(string name, Color colour)
        {
            var path = MaterialsFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader not found.");
            material = new Material(shader) { name = name, color = colour, enableInstancing = true };
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
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetList(Object target, string field, IReadOnlyList<Object> values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AddUnique(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
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
