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
    /// contributed by the Helm installer; the cairn, the engine, the propeller, the mast, the mount plate, the rotor mast
    /// and the Clipper's sail mast, rigging net, wing sail and figurehead from their Blender models), their greybox prefabs, the ship designs config and installer, and the built-in designs (<see cref="ShipFrameDesigns"/>,
    /// written through the codec, so the files are in canonical form). Materials come from the look's library
    /// (<see cref="LookMaterials"/>, coloured from the palette). Every prefab's pivot is its anchor point: on the floor for a face part, at the node for a
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
        public const string CorsairPath = DesignsFolder + "Corsair.ship.json";
        public const string ClipperPath = DesignsFolder + "Clipper.ship.json";
        private const string ModelsFolder = "Assets/Models/ShipParts/";
        private const string RotorMastModelPath = ModelsFolder + "RotorMast/rotor_mast.fbx";
        public const string InstallerPath = "Assets/Resources/Installers/ShipDesignsFeatureInstaller.asset";
        private const string PrefabsFolder = "Assets/Prefabs/ShipParts/";
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
        public const string RotorMast = "hull.rotor_mast";
        public const string SailMast = "hull.sail_mast";
        public const string RiggingNet = "hull.rigging_net";
        public const string WingSail = "hull.wing_sail";
        public const string Figurehead = "hull.figurehead";

        /// <summary>A mount plate's thickness; it lies on the floor, its socket on top.</summary>
        private const float PlateThickness = 0.1f;

        [MenuItem("TinCan/Dev/Ship Designs/Build Assets")]
        public static void Build()
        {
            var wood = LookMaterials.Get("M_ShipHull");
            var brass = LookMaterials.Get("M_ShipEngine");
            var stone = LookMaterials.Get("M_ShipCairn");
            var spirit = LookMaterials.SpiritFlame();

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
            // A mast 7.9 m tall on its node with two lift rotors on a vertical axis (3 m across at 3.6 m, 4.8 m across at
            // 6.35 m) and two pennants streaming aft (-Z); the Blender model (.tools/blender/rotor_mast.py). Mostly flavour.
            var rotorMast = Part("PART_RotorMast", RotorMast, "Rotor mast", "Hull", ShipPartAttach.Node, RotorMastPrefab(brass, wood), null,
                Vector3.zero, new ShipPartStats(15f, lift: 150f, hull: 15f), RotorMastFootprint());
            // The Clipper's sail parts (.tools/blender/sail_parts.py; blocks from each manifest). Mostly flavour: the sails
            // add a little thrust, the wings lift like a rotor mast (the Clipper's big hull needs it). A box round the mast's
            // pole is the only solid piece.
            var sailMast = Part("PART_SailMast", SailMast, "Sail mast", "Hull", ShipPartAttach.Node,
                SailPartPrefab("SailMast", "sail_mast", new Bounds(new Vector3(0f, 7f, 0f), new Vector3(0.4f, 14f, 0.4f))), null, Vector3.zero,
                new ShipPartStats(15f, thrust: 150f, hull: 20f), ManifestFootprint("SailMast"));
            var riggingNet = Part("PART_RiggingNet", RiggingNet, "Rigging net", "Hull", ShipPartAttach.Node, SailPartPrefab("RiggingNet", "rigging_net", null),
                null, Vector3.zero, new ShipPartStats(3f, hull: 5f), ManifestFootprint("RiggingNet"));
            var wingSail = Part("PART_WingSail", WingSail, "Wing sail", "Hull", ShipPartAttach.Node, SailPartPrefab("WingSail", "wing_sail", null),
                null, Vector3.zero, new ShipPartStats(10f, lift: 150f, hull: 10f), ManifestFootprint("WingSail"));
            var figurehead = Part("PART_Figurehead", Figurehead, "Figurehead", "Hull", ShipPartAttach.Node, SailPartPrefab("Figurehead", "figurehead", null),
                null, Vector3.zero, new ShipPartStats(5f, hull: 10f), ManifestFootprint("Figurehead"));
            // The cairn: the hearth where the ship's spirit lives, a fiery wisp that lifts the ship (ShipStatsTuning.SpiritLift).
            var cairn = Part("PART_Cairn", Cairn, "Spirit hearth", "Spirit", ShipPartAttach.Face, CairnPrefab(stone, spirit), null, Vector3.zero,
                new ShipPartStats(40f, hull: 40f), new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 0));
            SetFlag(cairn, "_isCairn");
            // The helm console (.tools/blender/helm.py) fits one block, but keeps the 3 x 2 x 5 blocks of the pirate stand it
            // replaced, so the designs and the room round the helmsman hold.
            var helm = Part("PART_CoreHelm", CoreHelm, "Helm", "Core", ShipPartAttach.Face, null, Load<GameObject>(HelmStationPath), Vector3.zero,
                new ShipPartStats(30f, hull: 100f), Box(3, 2, 5));
            SetFlag(helm, "_isCore");

            var starter = WriteDesign(StarterPath, ShipFrameDesigns.StarterDesign());
            var galleon = WriteDesign(GalleonPath, ShipFrameDesigns.GalleonDesign());
            var raft = WriteDesign(RaftPath, ShipFrameDesigns.RaftDesign());
            var corsair = WriteDesign(CorsairPath, ShipFrameDesigns.CorsairDesign());
            var clipper = WriteDesign(ClipperPath, ShipFrameDesigns.ClipperDesign());
            var config = Asset<ShipDesignsConfig>(ConfigPath, designs =>
            {
                designs.BuiltInDesigns = new List<TextAsset> { starter, galleon, raft, corsair, clipper };
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
                SetList(i, "_parts", new Object[] { cairn, engine, propeller, mast, mount, rotorMast, sailMast, riggingNet, wingSail, figurehead });
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
                      + $"{ShipFrameDesigns.GalleonDesign()}, {ShipFrameDesigns.RaftDesign()}, {ShipFrameDesigns.CorsairDesign()}, "
                      + $"{ShipFrameDesigns.ClipperDesign()}).");
        }

        /// <summary>The config's frame materials, and defaults for limits and tuning added after the asset was made.</summary>
        private static void Configure(ShipDesignsConfig designs)
        {
            designs.FramePlanks = LookMaterials.Get("M_ShipDeck");
            designs.FrameBeams = LookMaterials.Get("M_ShipHull");
            designs.FrameWalls = LookMaterials.Get("M_ShipWalls");

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


        /// <summary>
        /// The spirit's hearth, open toward +Z: a stone plinth, back wall and cheeks, a timber mantel, a hood and flue
        /// rising behind it, an iron grate with two logs on glowing embers, and on them the spirit, a fiery wisp drawn by
        /// its shader (<see cref="LookMaterials.SpiritFlame"/>) on a quad, with the warm light it throws on the room. Its solid
        /// pieces fit its 1 x 2 x 1 blocks; the flue rises a little above them. Plan: .docs/plans/spirit-talents.md.
        /// </summary>
        private static GameObject CairnPrefab(Material stone, Material spirit)
        {
            var wood = LookMaterials.Get("M_ShipHull");
            var logs = LookMaterials.Get("M_ShipWalls");
            var iron = LookMaterials.Get("M_ShipIron");
            var ember = LookMaterials.Get("M_CampEmber");

            var root = new GameObject("ShipPart_Cairn");
            Piece(root, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(1f, 0.24f, 0.9f), stone, solid: true);
            Piece(root, "BackWall", PrimitiveType.Cube, new Vector3(0f, 0.74f, -0.36f), new Vector3(1f, 1f, 0.16f), stone, solid: true);
            Piece(root, "CheekLeft", PrimitiveType.Cube, new Vector3(-0.42f, 0.74f, 0.02f), new Vector3(0.16f, 1f, 0.62f), stone, solid: true);
            Piece(root, "CheekRight", PrimitiveType.Cube, new Vector3(0.42f, 0.74f, 0.02f), new Vector3(0.16f, 1f, 0.62f), stone, solid: true);
            Piece(root, "Mantel", PrimitiveType.Cube, new Vector3(0f, 1.29f, 0.02f), new Vector3(1f, 0.1f, 0.84f), wood, solid: true);
            Piece(root, "Hood", PrimitiveType.Cube, new Vector3(0f, 1.62f, -0.14f), new Vector3(0.8f, 0.56f, 0.5f), stone, solid: true);
            Piece(root, "Flue", PrimitiveType.Cube, new Vector3(0f, 2.2f, -0.2f), new Vector3(0.44f, 0.6f, 0.34f), stone, solid: false);
            Piece(root, "Grate", PrimitiveType.Cube, new Vector3(0f, 0.28f, 0.02f), new Vector3(0.6f, 0.04f, 0.42f), iron, solid: false);
            Piece(root, "Embers", PrimitiveType.Sphere, new Vector3(0f, 0.31f, 0.02f), new Vector3(0.5f, 0.05f, 0.32f), ember, solid: false);
            Piece(root, "LogLeft", PrimitiveType.Cylinder, new Vector3(-0.08f, 0.35f, 0.03f), new Vector3(0.11f, 0.24f, 0.11f), logs, solid: false,
                Quaternion.Euler(0f, 25f, 90f));
            Piece(root, "LogRight", PrimitiveType.Cylinder, new Vector3(0.09f, 0.37f, 0f), new Vector3(0.1f, 0.22f, 0.1f), logs, solid: false,
                Quaternion.Euler(0f, -30f, 90f));
            Piece(root, "Spirit", PrimitiveType.Quad, new Vector3(0f, 0.78f, 0.04f), new Vector3(0.72f, 0.95f, 1f), spirit, solid: false);
            var spiritRenderer = root.transform.Find("Spirit").GetComponent<MeshRenderer>();
            spiritRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spiritRenderer.receiveShadows = false;

            var glow = new GameObject("HearthLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.8f, 0.4f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LookPalette.Load()["lantern-amber"];
            light.range = 6f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            glow.AddComponent<TinCan.Features.ShipSpirit.SpiritHearthLightView>();
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

        /// <summary>
        /// The rotor mast's blocks: the pole (eight up), the low rotor's 3 x 3 layer and the high rotor's 5 x 5, and the
        /// two pennants' blocks behind the pole (-Z).
        /// </summary>
        private static Vector3Int[] RotorMastFootprint() =>
            Enumerable.Range(0, 8).Select(y => new Vector3Int(0, y, 0))
                .Concat(from x in Enumerable.Range(-1, 3) from z in Enumerable.Range(-1, 3) where x != 0 || z != 0 select new Vector3Int(x, 3, z))
                .Concat(from x in Enumerable.Range(-2, 5) from z in Enumerable.Range(-2, 5) where x != 0 || z != 0 select new Vector3Int(x, 6, z))
                .Append(new Vector3Int(0, 4, -1)).Append(new Vector3Int(0, 7, -1))
                .ToArray();

        /// <summary>
        /// The Blender rotor mast wrapped as a part: its pivot on the node, its imported materials swapped for the shared
        /// ship materials (so the parts batch), and a box round the pole the only collider; the rotors and pennants are not
        /// solid. RotorLow and RotorHigh spin about local Y (static for now).
        /// </summary>
        private static GameObject RotorMastPrefab(Material brass, Material wood) =>
            ModelPrefab("ShipPart_RotorMast", RotorMastModelPath, "rotor_mast.py", new Dictionary<string, Material>
            {
                ["RotorMast_Iron"] = LookMaterials.Get("M_ShipIron"),
                ["RotorMast_Brass"] = brass,
                ["RotorMast_Wood"] = wood,
                ["RotorMast_Canvas"] = LookMaterials.Get("M_ShipCanvas"),
                ["RotorMast_Pennant"] = LookMaterials.Get("M_ShipPennant"),
            }, new Bounds(new Vector3(0f, 3.9f, 0f), new Vector3(0.4f, 7.8f, 0.4f)));

        /// <summary>
        /// One of the Clipper's sail parts (.tools/blender/sail_parts.py: the sail mast, the rigging net, the wing sail, the
        /// figurehead) wrapped as a part, its Sail_* materials swapped for the ship's; only the given box is solid.
        /// </summary>
        private static GameObject SailPartPrefab(string folder, string stem, Bounds? solid) =>
            ModelPrefab("ShipPart_" + folder, $"{ModelsFolder}{folder}/{stem}.fbx", "sail_parts.py", new Dictionary<string, Material>
            {
                ["Sail_Wood"] = LookMaterials.Get("M_ShipHull"),
                ["Sail_Brass"] = LookMaterials.Get("M_ShipEngine"),
                ["Sail_Iron"] = LookMaterials.Get("M_ShipIron"),
                ["Sail_Sail"] = LookMaterials.Get("M_ShipSail"),
                ["Sail_Trim"] = LookMaterials.Get("M_ShipCanvas"),
                ["Sail_Rope"] = LookMaterials.Get("M_ShipDeck"),
            }, solid);

        /// <summary>
        /// A Blender model wrapped as a part: its pivot on the anchor, its imported materials swapped for the shared ship
        /// materials by name (so the parts batch), and the given box its only collider (none: not solid).
        /// </summary>
        private static GameObject ModelPrefab(string name, string modelPath, string script, IReadOnlyDictionary<string, Material> shared, Bounds? solid)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter
                           ?? throw new InvalidOperationException($"Missing model {modelPath}: run .tools/blender/{script}.");
            importer.globalScale = 1f;
            importer.addCollider = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();

            var root = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(modelPath), root.transform);
            model.name = "Mesh";
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(m => m != null && shared.TryGetValue(m.name, out var ship) ? ship : m)
                    .ToArray();
            }

            if (solid is { } box)
            {
                var collider = root.AddComponent<BoxCollider>();
                collider.center = box.center;
                collider.size = box.size;
            }

            return Save(root);
        }

        /// <summary>A Blender part's blocks, as its script worked them out from the geometry (manifest.json, "footprint").</summary>
        private static Vector3Int[] ManifestFootprint(string folder)
        {
            var path = $"{ModelsFolder}{folder}/manifest.json";
            var manifest = JsonUtility.FromJson<PartManifest>(File.ReadAllText(Path.GetFullPath(path)));
            if (manifest?.footprint == null || manifest.footprint.Count == 0)
                throw new InvalidOperationException($"{path} lists no footprint: run .tools/blender/sail_parts.py.");
            return manifest.footprint.Select(b => new Vector3Int(b.x, b.y, b.z)).ToArray();
        }

        [Serializable]
        private sealed class PartManifest
        {
            public List<ManifestBlock> footprint = new();
        }

        [Serializable]
        private sealed class ManifestBlock
        {
            public int x;
            public int y;
            public int z;
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
