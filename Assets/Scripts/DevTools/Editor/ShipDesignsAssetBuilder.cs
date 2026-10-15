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
    /// TinCan > Dev > Ship Designs > Build Assets: the ship parts (hull block, deck tile, and the helm as the core part,
    /// contributed by the Helm installer; the parts pack's stairs, rail, wall and propeller), their greybox prefabs and
    /// materials, the ship designs config and installer, and the built-in designs (written through the codec, so the
    /// files are in canonical form). Materials are created only when missing, so their colours stay as tuned. Plans:
    /// .docs/plans/modular-airship-builder.md, .docs/plans/airship-parts-pack.md.
    /// </summary>
    public static class ShipDesignsAssetBuilder
    {
        public const string PartsFolder = "Assets/Settings/ShipParts/";
        public const string DesignsFolder = "Assets/Settings/ShipDesigns/";
        public const string ConfigPath = DesignsFolder + "ShipDesignsConfig.asset";
        public const string StarterPath = DesignsFolder + "Starter.ship.json";
        public const string GalleonPath = DesignsFolder + "Galleon.ship.json";
        public const string InstallerPath = "Assets/Resources/Installers/ShipDesignsFeatureInstaller.asset";
        private const string PrefabsFolder = "Assets/Prefabs/ShipParts/";
        private const string MaterialsFolder = "Assets/Materials/ShipParts/";
        private const string GreyboxMeshesFolder = "Assets/Models/ShipParts/Greybox/";
        private const string HelmInstallerPath = "Assets/Resources/Installers/HelmFeatureInstaller.asset";
        private const string HelmStationPath = "Assets/Prefabs/Airship/Parts/HelmStation.prefab";
        private const string StationsInstallerPath = "Assets/Resources/Installers/StationsFeatureInstaller.asset";
        private const string StatePrefabPath = "Assets/Prefabs/ShipDesigns/ShipDesignState.prefab";
        private const string StateFixturePath = "Assets/Settings/Fixtures/ShipDesignStateFixture.asset";
        private const string TestShipPath = "Assets/Prefabs/Test/TestShip_Prefab.prefab";
        public const string ModularShipPath = "Assets/Prefabs/Airship/ModularShip_Prefab.prefab";
        public const string ProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Shipyard.asset";
        private const string CoreProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Core.asset";

        public const string HullBlock = "hull.block";
        public const string HullDeck = "hull.deck";
        public const string CoreHelm = "core.helm";
        public const string HullArmour = "hull.armour";
        public const string Engine = "prop.engine";
        public const string Balloon = "lift.balloon";
        public const string Mast = "hull.mast";
        public const string Envelope = "lift.envelope";
        public const string HullMount = "hull.mount";
        public const string Stairs = "hull.stairs";
        public const string Rail = "hull.rail";
        public const string Wall = "hull.wall";
        public const string WallWindow = "hull.wall_window";
        public const string Bow = "hull.bow";
        public const string Propeller = "prop.propeller";

        /// <summary>A deck tile's thickness; its top is flush with the top of its cell, the cell's walkable surface.</summary>
        private const float DeckThickness = 0.2f;

        // Yaw orientations (ShipPartOrientation: k quarter turns clockwise seen from above). A rail or wall stands on its
        // cell's +X edge, which faces +X, -Z, -X, +Z for k = 0..3; stairs rise toward +Z for 0 and toward -Z for 2.
        private const byte FacesPlusX = 0, FacesMinusZ = 1, FacesMinusX = 2, FacesPlusZ = 3;
        private const byte RisesPlusZ = 0, RisesMinusZ = 2;

        [MenuItem("TinCan/Dev/Ship Designs/Build Assets")]
        public static void Build()
        {
            var wood = Material("M_ShipHull", new Color(0.45f, 0.30f, 0.18f));
            var planks = Material("M_ShipDeck", new Color(0.66f, 0.52f, 0.34f));

            var blockPrefab = BoxPrefab("ShipPart_HullBlock", Vector3.zero, Vector3.one * ShipGrid.CellSize, wood);
            var deckPrefab = BoxPrefab("ShipPart_HullDeck",
                new Vector3(0f, (ShipGrid.CellSize - DeckThickness) * 0.5f, 0f),
                new Vector3(ShipGrid.CellSize, DeckThickness, ShipGrid.CellSize), planks);

            var steel = Material("M_ShipArmour", new Color(0.32f, 0.34f, 0.38f));
            var brass = Material("M_ShipEngine", new Color(0.55f, 0.42f, 0.2f));
            var canvas = Material("M_ShipBalloon", new Color(0.86f, 0.82f, 0.72f));
            var armourPrefab = BoxPrefab("ShipPart_HullArmour", Vector3.zero, Vector3.one * ShipGrid.CellSize, steel);
            // The engine fills its cell and the one behind it (-Z).
            var enginePrefab = BoxPrefab("ShipPart_Engine", new Vector3(0f, 0f, -0.5f), new Vector3(0.9f, 0.9f, 2f), brass);
            // The balloon fills 3 x 2 x 3 cells around its origin, one layer up.
            var balloonPrefab = BoxPrefab("ShipPart_Balloon", new Vector3(0f, 0.5f, 0f), new Vector3(3f, 2f, 3f), canvas, PrimitiveType.Sphere);

            var block = Part("PART_HullBlock", HullBlock, "Hull block", "Hull", false, blockPrefab, null, Vector3.zero, new ShipPartStats(10f, hull: 25f));
            var deck = Part("PART_HullDeck", HullDeck, "Deck tile", "Hull", false, deckPrefab, null, Vector3.zero, new ShipPartStats(3f, hull: 5f));
            var armour = Part("PART_HullArmour", HullArmour, "Armour block", "Hull", false, armourPrefab, null, Vector3.zero, new ShipPartStats(30f, hull: 120f));
            var engine = Part("PART_Engine", Engine, "Engine", "Drive", false, enginePrefab, null, Vector3.zero, new ShipPartStats(40f, thrust: 400f, hull: 40f),
                new Vector3Int(0, 0, 0), new Vector3Int(0, 0, -1));
            var balloon = Part("PART_Balloon", Balloon, "Balloon", "Lift", false, balloonPrefab, null, Vector3.zero, new ShipPartStats(15f, lift: 400f, hull: 10f),
                Box(3, 2, 3));
            // A post one cell tall: stack them to hold an envelope above the deck.
            var mastPrefab = BoxPrefab("ShipPart_Mast", Vector3.zero, new Vector3(0.25f, 1f, 0.25f), wood);
            var mast = Part("PART_Mast", Mast, "Mast", "Hull", false, mastPrefab, null, Vector3.zero, new ShipPartStats(5f, hull: 10f));
            // The big top balloon: 5 x 3 x 7 cells, lifting a whole ship from above the deck.
            var envelopePrefab = BoxPrefab("ShipPart_Envelope", new Vector3(0f, 1f, 0f), new Vector3(4.8f, 2.8f, 6.8f), canvas, PrimitiveType.Sphere);
            var envelope = Part("PART_Envelope", Envelope, "Envelope", "Lift", false, envelopePrefab, null, Vector3.zero,
                new ShipPartStats(40f, lift: 1200f, hull: 30f), Box(5, 3, 7));
            // A deck tile with a socket on top: where fittings (a cannon, a tool rack) are mounted.
            var mount = Part("PART_HullMount", HullMount, "Mount plate", "Hull", false, MountPrefab(brass), null, Vector3.zero, new ShipPartStats(5f, hull: 10f));
            // The helm stand is about 4.5 x 2.2 x 3.9 m with its pivot at its base, reaching 2.35 m forward: 3 x 2 x 5 cells,
            // standing on the floor of its origin cell.
            var helm = Part("PART_CoreHelm", CoreHelm, "Helm", "Core", true, null, Load<GameObject>(HelmStationPath),
                new Vector3(0f, -0.5f * ShipGrid.CellSize, 0f), new ShipPartStats(30f, hull: 100f), Box(3, 2, 5));

            // The parts pack (.docs/plans/airship-parts-pack.md): greybox until the Blender meshes replace the visuals.
            // Stairs: 3 m up over 5 m (31°, under the player's 45° slope limit); the cells above the slope are the stairwell.
            var stairs = Part("PART_Stairs", Stairs, "Stairs", "Hull", false, StairsPrefab(planks), null, Vector3.zero,
                new ShipPartStats(15f, hull: 15f), Cells(1, 3, 5));
            var rail = Part("PART_Rail", Rail, "Rail", "Hull", false, RailPrefab(wood), null, Vector3.zero, new ShipPartStats(2f, hull: 3f));
            // A wall as tall as a level (3 cells), capped by a deck slab: it carries the deck above it.
            var wall = Part("PART_Wall", Wall, "Hull wall", "Hull", false, WallPrefab(wood, planks), null, Vector3.zero,
                new ShipPartStats(6f, hull: 15f), Cells(1, 3, 1));
            // The same wall with a window at eye height.
            var windowWall = Part("PART_WallWindow", WallWindow, "Hull wall (window)", "Hull", false, WallPrefab(wood, planks, window: true), null,
                Vector3.zero, new ShipPartStats(5f, hull: 12f), Cells(1, 3, 1));
            // A prow for a hull 9 cells wide and 5 deep (keel to main deck): it tapers to a point 4 cells ahead, its top
            // is deck, railed along the edges, with a bowsprit reaching up and forward.
            var bow = Part("PART_Bow", Bow, "Bow", "Hull", false, BowPrefab(wood, planks), null, Vector3.zero,
                new ShipPartStats(60f, hull: 120f), BowFootprint());
            // A nacelle in its cell and a 3 x 3 propeller disc one cell behind it (-Z).
            var propeller = Part("PART_Propeller", Propeller, "Propeller", "Drive", false, PropellerPrefab(brass, wood), null, Vector3.zero,
                new ShipPartStats(25f, thrust: 400f, hull: 20f),
                new[] { Vector3Int.zero }.Concat(from x in Enumerable.Range(-1, 3) from y in Enumerable.Range(-1, 3) select new Vector3Int(x, y, -1)).ToArray());

            var starter = WriteDesign(StarterPath, StarterDesign());
            var galleon = WriteDesign(GalleonPath, GalleonDesign());
            var config = Asset<ShipDesignsConfig>(ConfigPath, designs =>
            {
                designs.BuiltInDesigns = new List<TextAsset> { starter, galleon };
                designs.DefaultDesign = "Starter";
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
                SetList(i, "_parts", new Object[] { block, deck, armour, engine, balloon, mast, envelope, mount, stairs, rail, wall, windowWall, bow, propeller });
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
            Debug.Log($"[ShipDesignsAssetBuilder] Ship design assets built ({installer.name}, starter design with {StarterDesign().Parts.Count} parts, "
                      + $"galleon with {GalleonDesign().Parts.Count}).");
        }

        /// <summary>
        /// The starter ship: a 5 x 9 deck with a keel under it, gunwales along both sides, the helm midships, an
        /// engine off the stern and an envelope on four masts above the deck (mass 675, lift 1200, thrust 400: about 14 m/s).
        /// Cell y 0 is the deck's walking level.
        /// </summary>
        public static ShipDesign StarterDesign()
        {
            var parts = new List<(string Part, ShipGridCell Cell)>();
            // Two mount plates in the bow deck: sockets for fittings.
            for (int x = -2; x <= 2; x++)
            for (int z = -4; z <= 4; z++)
                parts.Add((z == 4 && x is -1 or 1 ? HullMount : HullDeck, new ShipGridCell(x, -1, z)));
            for (int x = -1; x <= 1; x++)
            for (int z = -3; z <= 3; z++)
                parts.Add((HullBlock, new ShipGridCell(x, -2, z)));
            for (int z = -4; z <= 4; z++)
            {
                parts.Add((HullBlock, new ShipGridCell(-2, 0, z)));
                parts.Add((HullBlock, new ShipGridCell(2, 0, z)));
            }

            // Midships: the helmsman stands 2 m behind the wheel, which must still be deck.
            parts.Add((CoreHelm, new ShipGridCell(0, 0, 0)));
            // An engine off the stern.
            parts.Add((Engine, new ShipGridCell(0, -1, -5)));
            // Four masts, two cells tall, on the gunwales, holding the envelope 3 m above the deck.
            foreach (int x in new[] { -2, 2 })
            foreach (int z in new[] { -2, 2 })
            {
                parts.Add((Mast, new ShipGridCell(x, 1, z)));
                parts.Add((Mast, new ShipGridCell(x, 2, z)));
            }

            parts.Add((Envelope, new ShipGridCell(0, 3, 0)));

            var placements = parts.Select((p, i) => new ShipPartPlacement(i + 1, p.Part, p.Cell, 0));
            return new ShipDesign("Starter", "TinCan", parts.Count + 1, placements);
        }

        /// <summary>
        /// The galleon: three levels on a hull 9 cells wide and 16 long, with a pointed bow at +Z. Decks are 3 cells apart
        /// (2.8 m headroom under a 0.2 m slab). Hold: floor at y -4, walled all round with windows, reached by a railed
        /// hatch staircase 3 wide from the main deck. Main deck: y -1, railed sides, two staircases aft up to the
        /// quarterdeck, two masts carrying the envelope. Quarterdeck: y 2 over a cabin with windows, the helm in the
        /// middle; the stairs land in lanes clear of the helm stand (4.5 m wide, wider than its 3 cells). Four side
        /// balloons, three propellers under the stern, mount plates on every level. Plan: .docs/plans/airship-parts-pack.md.
        /// </summary>
        public static ShipDesign GalleonDesign()
        {
            var parts = new List<(string Part, ShipGridCell Cell, byte Orientation)>();
            void Add(string part, int x, int y, int z, byte orientation = 0) => parts.Add((part, new ShipGridCell(x, y, z), orientation));
            // Side walls alternate plain and window, so every other cell looks out.
            void SideWall(int x, int y, int z, byte orientation) => Add(Math.Abs(z) % 2 == 1 ? WallWindow : Wall, x, y, z, orientation);

            // Keel (to the bow), and the hold floor with two pairs of mount plates.
            for (int x = -1; x <= 1; x++)
            for (int z = -6; z <= 7; z++)
                Add(HullBlock, x, -5, z);
            for (int x = -4; x <= 4; x++)
            for (int z = -8; z <= 7; z++)
                Add(x is -3 or 3 && z is -5 or 3 ? HullMount : HullDeck, x, -4, z);

            // The hold's walls (y -3..-1, their caps are the main deck's edge), solid blocks at the corners.
            for (int z = -7; z <= 6; z++)
            {
                SideWall(4, -3, z, FacesPlusX);
                SideWall(-4, -3, z, FacesMinusX);
            }

            for (int x = -3; x <= 3; x++)
            {
                Add(Wall, x, -3, 7, FacesPlusZ);
                Add(Wall, x, -3, -8, FacesMinusZ);
            }

            Corners(-3);

            // Main deck, open over the hatch: three staircases side by side (x -1..1) rising aft from the hold floor at
            // z 6 to the main deck at z 1, railed along both sides and across the deep end.
            for (int x = -3; x <= 3; x++)
            for (int z = -7; z <= 6; z++)
            {
                if (x is >= -1 and <= 1 && z is >= 2 and <= 6) continue;
                Add(z == 6 && x is -3 or 3 ? HullMount : HullDeck, x, -1, z);
            }

            for (int x = -1; x <= 1; x++)
            {
                Add(Stairs, x, -3, 6, RisesMinusZ);
                Add(Rail, x, 0, 7, FacesMinusZ);
            }

            for (int z = 2; z <= 6; z++)
            {
                Add(Rail, -2, 0, z, FacesPlusX);
                Add(Rail, 2, 0, z, FacesMinusX);
            }

            // The cabin under the quarterdeck: walls at the sides and stern (caps are the quarterdeck's edge), open forward.
            for (int z = -7; z <= -2; z++)
            {
                SideWall(4, 0, z, FacesPlusX);
                SideWall(-4, 0, z, FacesMinusX);
            }

            for (int x = -3; x <= 3; x++)
                Add(Wall, x, 0, -8, FacesMinusZ);
            Corners(0);

            // Main deck rails forward of the cabin, to the bow.
            for (int z = -1; z <= 7; z++)
            {
                Add(Rail, 4, 0, z, FacesPlusX);
                Add(Rail, -4, 0, z, FacesMinusX);
            }

            // The bow: a prow 4 cells long from the keel up to the main deck, its top flush with the deck.
            Add(Bow, 0, -5, 8);

            // Two staircases rising aft along the sides, from the main deck at z 4 to the quarterdeck's front row at z -2.
            Add(Stairs, 3, 0, 3, RisesMinusZ);
            Add(Stairs, -3, 0, 3, RisesMinusZ);

            // Quarterdeck with two mount plates aft, rails on every edge but the stair landings, and the helm (the
            // helmsman stands about z -7).
            for (int x = -3; x <= 3; x++)
            for (int z = -7; z <= -2; z++)
                Add(z == -7 && x is -3 or 3 ? HullMount : HullDeck, x, 2, z);
            for (int z = -8; z <= -2; z++)
            {
                Add(Rail, 4, 3, z, FacesPlusX);
                Add(Rail, -4, 3, z, FacesMinusX);
            }

            for (int x = -3; x <= 3; x++)
                Add(Rail, x, 3, -8, FacesMinusZ);
            for (int x = -2; x <= 2; x++)
                Add(Rail, x, 3, -2, FacesPlusZ);
            Add(CoreHelm, 0, 3, -5);

            // Two masts, five cells tall, holding the envelope over the main deck clear of heads on the stairs.
            for (int y = 0; y <= 4; y++)
            {
                Add(Mast, 2, y, 0);
                Add(Mast, -2, y, 0);
            }

            Add(Envelope, 0, 5, 2);

            // Side balloons along the hold walls, propellers under the stern.
            foreach (int x in new[] { -6, 6 })
            foreach (int z in new[] { -4, 2 })
                Add(Balloon, x, -3, z);
            foreach (int x in new[] { -3, 0, 3 })
                Add(Propeller, x, -3, -9);

            var placements = parts.Select((p, i) => new ShipPartPlacement(i + 1, p.Part, p.Cell, p.Orientation));
            return new ShipDesign("Galleon", "TinCan", parts.Count + 1, placements);

            // Solid blocks filling a level's four corner columns (3 cells from the given level up).
            void Corners(int bottom)
            {
                for (int y = bottom; y <= bottom + 2; y++)
                foreach (int x in new[] { -4, 4 })
                foreach (int z in new[] { -8, 7 })
                {
                    if (bottom == 0 && z == 7) continue; // the main deck's bow is open: rails, not a cabin
                    Add(HullBlock, x, y, z);
                }
            }
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

        /// <summary>Cells from the origin cell up (+Y) and forward (+Z), centred across X.</summary>
        private static Vector3Int[] Cells(int width, int height, int depth) =>
            (from x in Enumerable.Range(-(width / 2), width) from y in Enumerable.Range(0, height) from z in Enumerable.Range(0, depth)
                select new Vector3Int(x, y, z)).ToArray();

        /// <summary>
        /// Ten steps, 0.3 m up and 0.5 m deep, from the floor of the origin cell to the top of the cell 3 up and 5
        /// forward. The steps are only seen; feet ride one ramp collider through the middle of the step noses, which
        /// starts at the floor just before the first step and ends 0.15 m above the deck it reaches.
        /// </summary>
        private static GameObject StairsPrefab(Material material)
        {
            const int steps = 10;
            const float rise = 0.3f, tread = 0.5f, floor = -0.5f;
            var root = new GameObject("ShipPart_Stairs");
            for (int i = 0; i < steps; i++)
            {
                float height = rise * (i + 1);
                Piece(root, $"Step_{i:00}", PrimitiveType.Cube, new Vector3(0f, floor + height * 0.5f, floor + tread * (i + 0.5f)),
                    new Vector3(1f, height, tread), material, solid: false);
            }

            // The ramp's top surface: y = floor + rise/2 + slope * (z - floor), from the floor up to the last step's far edge.
            float slope = rise / tread;
            var start = new Vector3(0f, floor, floor - rise * 0.5f / slope);
            var end = new Vector3(0f, floor + rise * 0.5f + slope * tread * steps, floor + tread * steps);
            const float thickness = 0.1f;
            var along = (end - start).normalized;
            var normal = Vector3.Cross(along, Vector3.right).normalized;
            var ramp = new GameObject("Ramp");
            ramp.transform.SetParent(root.transform, false);
            ramp.transform.localPosition = (start + end) * 0.5f - normal * thickness * 0.5f;
            ramp.transform.localRotation = Quaternion.LookRotation(along, normal);
            ramp.AddComponent<BoxCollider>().size = new Vector3(1f, thickness, (end - start).magnitude);
            return Save(root);
        }

        /// <summary>Two posts and two rails along the cell's +X edge, 1 m high; one box keeps players in.</summary>
        private static GameObject RailPrefab(Material material)
        {
            const float edge = 0.45f, floor = -0.5f;
            var root = new GameObject("ShipPart_Rail");
            foreach (float z in new[] { -0.45f, 0.45f })
                Piece(root, "Post", PrimitiveType.Cube, new Vector3(edge, floor + 0.5f, z), new Vector3(0.08f, 1f, 0.08f), material, solid: false);
            Piece(root, "TopRail", PrimitiveType.Cube, new Vector3(edge, floor + 1f, 0f), new Vector3(0.1f, 0.08f, 1f), material, solid: false);
            Piece(root, "MidRail", PrimitiveType.Cube, new Vector3(edge, floor + 0.5f, 0f), new Vector3(0.05f, 0.05f, 1f), material, solid: false);
            var block = new GameObject("Collider");
            block.transform.SetParent(root.transform, false);
            block.transform.localPosition = new Vector3(edge, floor + 0.55f, 0f);
            block.AddComponent<BoxCollider>().size = new Vector3(0.1f, 1.1f, 1f);
            return Save(root);
        }

        /// <summary>
        /// A 3 m wall along the +X edge of its three cells, with a deck slab on top flush with the level above. The panel
        /// stops under the slab: no two faces share a plane, so nothing flickers (z-fighting) along the deck's edge. With
        /// a window, the panel is four pieces around a 0.5 x 0.8 m opening 1.1 m above the floor.
        /// </summary>
        private static GameObject WallPrefab(Material wall, Material deck, bool window = false)
        {
            const float x = 0.4f, thickness = 0.2f, floor = -0.5f;
            float top = 2.5f - DeckThickness;
            var root = new GameObject(window ? "ShipPart_WallWindow" : "ShipPart_Wall");
            if (window)
            {
                const float sill = floor + 1.1f, lintel = sill + 0.8f, half = 0.25f;
                Piece(root, "Below", PrimitiveType.Cube, new Vector3(x, (floor + sill) * 0.5f, 0f), new Vector3(thickness, sill - floor, 1f), wall, solid: true);
                Piece(root, "Above", PrimitiveType.Cube, new Vector3(x, (lintel + top) * 0.5f, 0f), new Vector3(thickness, top - lintel, 1f), wall, solid: true);
                foreach (float side in new[] { -1f, 1f })
                {
                    Piece(root, "Jamb", PrimitiveType.Cube, new Vector3(x, (sill + lintel) * 0.5f, side * (0.5f + half) * 0.5f),
                        new Vector3(thickness, lintel - sill, 0.5f - half), wall, solid: true);
                }
            }
            else
            {
                Piece(root, "Panel", PrimitiveType.Cube, new Vector3(x, (floor + top) * 0.5f, 0f), new Vector3(thickness, top - floor, 1f), wall, solid: true);
            }

            Piece(root, "Cap", PrimitiveType.Cube, new Vector3(0f, top + DeckThickness * 0.5f, 0f), new Vector3(1f, DeckThickness, 1f), deck, solid: true);
            return Save(root);
        }

        /// <summary>
        /// The bow's cells: a 9 x 6 x 4 box (keel to main deck, plus the layer its rails stand in, 4 cells ahead) and the
        /// cells the bowsprit passes through.
        /// </summary>
        private static Vector3Int[] BowFootprint() =>
            Box(9, 6, 1).SelectMany(c => Enumerable.Range(0, 4).Select(z => new Vector3Int(c.x, c.y, z)))
                .Concat(new[] { new Vector3Int(0, 5, 4), new Vector3Int(0, 5, 5), new Vector3Int(0, 6, 6), new Vector3Int(0, 6, 7) })
                .ToArray();

        /// <summary>
        /// The prow, in the origin cell's space (origin at the keel, one cell ahead of the hull): a solid that is the deck
        /// outline at the top (9 m wide at the hull, narrowing to a point 4 m ahead) and a keel-wide triangle at the bottom.
        /// Its top is deck (planks), its sides hull; a convex collider of the same mesh. Rails run along the four outer
        /// edges of the top, and a bowsprit reaches up and forward from the point.
        /// </summary>
        private static GameObject BowPrefab(Material hull, Material deck)
        {
            const float back = -0.5f, top = 4.5f, bottom = -0.5f;
            Vector3[] outline =
            {
                new(-4.5f, top, back), new(-3.3f, top, back + 2f), new(0f, top, back + 4f), new(3.3f, top, back + 2f), new(4.5f, top, back),
            };
            Vector3[] keel = { new(-1.5f, bottom, back), new(0f, bottom, back + 2f), new(1.5f, bottom, back) };

            var mesh = BowMesh(outline, keel);
            var root = new GameObject("ShipPart_Bow");
            var body = new GameObject("Mesh");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterials = new[] { deck, hull };
            body.AddComponent<MeshCollider>().sharedMesh = mesh;
            body.GetComponent<MeshCollider>().convex = true;

            // Rails along the outer edges (not the edge against the hull), set 0.05 m in from the edge.
            var centre = outline.Aggregate(Vector3.zero, (sum, p) => sum + p) / outline.Length;
            for (int i = 0; i < outline.Length - 1; i++)
            {
                var a = outline[i];
                var b = outline[i + 1];
                var inward = new Vector3(centre.x - (a.x + b.x) * 0.5f, 0f, centre.z - (a.z + b.z) * 0.5f).normalized * 0.05f;
                var middle = (a + b) * 0.5f + inward;
                var along = Quaternion.LookRotation(b - a, Vector3.up);
                float length = (b - a).magnitude;
                Piece(root, "TopRail", PrimitiveType.Cube, middle + Vector3.up * 1f, new Vector3(0.1f, 0.08f, length), hull, solid: false, along);
                Piece(root, "MidRail", PrimitiveType.Cube, middle + Vector3.up * 0.5f, new Vector3(0.05f, 0.05f, length), hull, solid: false, along);
                Piece(root, "Post", PrimitiveType.Cube, a + inward + Vector3.up * 0.5f, new Vector3(0.08f, 1f, 0.08f), hull, solid: false);
                var block = new GameObject("RailCollider");
                block.transform.SetParent(root.transform, false);
                block.transform.localPosition = middle + Vector3.up * 0.55f;
                block.transform.localRotation = along;
                block.AddComponent<BoxCollider>().size = new Vector3(0.1f, 1.1f, length);
            }

            var last = outline[^1];
            Piece(root, "Post", PrimitiveType.Cube, last + new Vector3(centre.x - last.x, 0f, centre.z - last.z).normalized * 0.05f + Vector3.up * 0.5f,
                new Vector3(0.08f, 1f, 0.08f), hull, solid: false);

            // The bowsprit: from the point, 4.5 m up and forward.
            var foot = new Vector3(0f, top + 0.3f, back + 3.7f);
            var reach = new Vector3(0f, 0.35f, 1f).normalized * 4.5f;
            Piece(root, "Bowsprit", PrimitiveType.Cylinder, foot + reach * 0.5f, new Vector3(0.22f, reach.magnitude * 0.5f, 0.22f), hull, solid: false,
                Quaternion.FromToRotation(Vector3.up, reach));
            return Save(root);
        }

        /// <summary>
        /// A flat-shaded solid from a convex top outline and a bottom keel outline (same depth at the back): the top as
        /// submesh 0, every other face as submesh 1. Each face is wound to face away from the solid's middle. Saved under
        /// the greybox meshes folder (kept in place when rebuilt, so references hold).
        /// </summary>
        private static Mesh BowMesh(Vector3[] outline, Vector3[] keel)
        {
            var middle = outline.Concat(keel).Aggregate(Vector3.zero, (sum, p) => sum + p) / (outline.Length + keel.Length);
            var vertices = new List<Vector3>();
            var topTriangles = new List<int>();
            var sideTriangles = new List<int>();
            void Face(List<int> into, Vector3 a, Vector3 b, Vector3 c)
            {
                // Unity's front face for (a, b, c) faces along Cross(b - a, c - a): turn it to face away from the middle.
                var normal = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(normal, (a + b + c) / 3f - middle) < 0f) (b, c) = (c, b);
                into.Add(vertices.Count); vertices.Add(a);
                into.Add(vertices.Count); vertices.Add(b);
                into.Add(vertices.Count); vertices.Add(c);
            }

            for (int i = 1; i < outline.Length - 1; i++) Face(topTriangles, outline[0], outline[i], outline[i + 1]);
            // outline: back-left, left, point, right, back-right; keel: back-left, point, back-right.
            Face(sideTriangles, outline[0], outline[4], keel[2]);
            Face(sideTriangles, outline[0], keel[2], keel[0]);
            Face(sideTriangles, outline[0], outline[1], keel[0]);
            Face(sideTriangles, outline[1], keel[1], keel[0]);
            Face(sideTriangles, outline[1], outline[2], keel[1]);
            Face(sideTriangles, outline[2], outline[3], keel[1]);
            Face(sideTriangles, outline[3], keel[2], keel[1]);
            Face(sideTriangles, outline[3], outline[4], keel[2]);
            Face(sideTriangles, keel[0], keel[1], keel[2]);

            var path = GreyboxMeshesFolder + "Bow.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            mesh ??= new Mesh { name = "Bow" };
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(topTriangles, 0);
            mesh.SetTriangles(sideTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (created)
            {
                EnsureFolder(path);
                AssetDatabase.CreateAsset(mesh, path);
            }

            EditorUtility.SetDirty(mesh);
            return mesh;
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

        private static ShipPartDefinition Part(string asset, string partId, string displayName, string category, bool isCore,
            GameObject? visual, GameObject? networkedPrefab, Vector3 pivotOffset, ShipPartStats stats, params Vector3Int[] footprint) =>
            Asset<ShipPartDefinition>(PartsFolder + asset + ".asset", part =>
            {
                var serialized = new SerializedObject(part);
                serialized.FindProperty("_partId").stringValue = partId;
                serialized.FindProperty("_displayName").stringValue = displayName;
                serialized.FindProperty("_category").stringValue = category;
                serialized.FindProperty("_isCore").boolValue = isCore;
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

        /// <summary>A deck tile in a mount colour, with a ShipSocket on its top face.</summary>
        private static GameObject MountPrefab(Material material)
        {
            var path = PrefabsFolder + "ShipPart_HullMount.prefab";
            var root = new GameObject("ShipPart_HullMount");
            try
            {
                var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "Mesh";
                plate.transform.SetParent(root.transform, false);
                plate.transform.localPosition = new Vector3(0f, (ShipGrid.CellSize - DeckThickness) * 0.5f, 0f);
                plate.transform.localScale = new Vector3(ShipGrid.CellSize, DeckThickness, ShipGrid.CellSize);
                plate.GetComponent<MeshRenderer>().sharedMaterial = material;
                var socket = new GameObject("ShipSocket");
                socket.transform.SetParent(root.transform, false);
                socket.transform.localPosition = new Vector3(0f, 0.5f * ShipGrid.CellSize, 0f);
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
