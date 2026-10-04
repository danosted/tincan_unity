#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Ship.Parts;
using TinCan.Features.Helm;
using TinCan.Features.ShipDesigns;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Ship Designs > Build Assets: the first ship parts (hull block, deck tile, and the helm as the core
    /// part, contributed by the Helm installer), their prefabs and materials, the ship designs config and installer,
    /// and the built-in starter design (written through the codec, so the file is in canonical form). Materials are
    /// created only when missing, so their colours stay as tuned. Plan: .docs/plans/modular-airship-builder.md.
    /// </summary>
    public static class ShipDesignsAssetBuilder
    {
        public const string PartsFolder = "Assets/Settings/ShipParts/";
        public const string DesignsFolder = "Assets/Settings/ShipDesigns/";
        public const string ConfigPath = DesignsFolder + "ShipDesignsConfig.asset";
        public const string StarterPath = DesignsFolder + "Starter.ship.json";
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

        public const string HullBlock = "hull.block";
        public const string HullDeck = "hull.deck";
        public const string CoreHelm = "core.helm";
        public const string HullArmour = "hull.armour";
        public const string Engine = "prop.engine";
        public const string Balloon = "lift.balloon";
        public const string Mast = "hull.mast";
        public const string Envelope = "lift.envelope";

        /// <summary>A deck tile's thickness; its top is flush with the top of its cell, the cell's walkable surface.</summary>
        private const float DeckThickness = 0.2f;

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
            // The helm stand is about 4.5 x 2.2 x 3.9 m with its pivot at its base: 3 x 2 x 3 cells, standing on the floor
            // of its origin cell.
            var helm = Part("PART_CoreHelm", CoreHelm, "Helm", "Core", true, null, Load<GameObject>(HelmStationPath),
                new Vector3(0f, -0.5f * ShipGrid.CellSize, 0f), new ShipPartStats(30f, hull: 100f), Box(3, 2, 3));

            var starter = WriteStarter();
            var config = Asset<ShipDesignsConfig>(ConfigPath, designs =>
            {
                designs.BuiltInDesigns = new List<TextAsset> { starter };
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
                SetList(i, "_parts", new Object[] { block, deck, armour, engine, balloon, mast, envelope });
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
            Debug.Log($"[ShipDesignsAssetBuilder] Ship design assets built ({installer.name}, starter design with {StarterDesign().Parts.Count} parts).");
        }

        /// <summary>
        /// The starter ship: a 5 x 9 deck with a keel under it, gunwales along both sides, the helm midships, an
        /// engine off the stern and an envelope on four masts above the deck (mass 675, lift 1200, thrust 400: about 14 m/s).
        /// Cell y 0 is the deck's walking level.
        /// </summary>
        public static ShipDesign StarterDesign()
        {
            var parts = new List<(string Part, ShipGridCell Cell)>();
            for (int x = -2; x <= 2; x++)
            for (int z = -4; z <= 4; z++)
                parts.Add((HullDeck, new ShipGridCell(x, -1, z)));
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

        private static TextAsset WriteStarter()
        {
            EnsureFolder(StarterPath);
            var text = new ShipDesignJsonCodec(ShipDesignLimits.Default).Encode(StarterDesign());
            File.WriteAllText(Path.GetFullPath(StarterPath), text);
            AssetDatabase.ImportAsset(StarterPath, ImportAssetOptions.ForceUpdate);
            return Load<TextAsset>(StarterPath);
        }

        private static Vector3Int[] Box(int width, int height, int depth) =>
            (from x in Enumerable.Range(-(width / 2), width) from y in Enumerable.Range(0, height) from z in Enumerable.Range(-(depth / 2), depth)
                select new Vector3Int(x, y, z)).ToArray();

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
