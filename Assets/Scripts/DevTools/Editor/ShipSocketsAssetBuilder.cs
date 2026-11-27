#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Interaction;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.ShipDesigns;
using TinCan.Features.ShipSockets;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Ship Sockets > Build Assets: IA_MountFitting, the sockets config, the ShipSocketsState prefab and
    /// fixture, the installer, the first fittings (the cannon station, from the Cannon feature; the tool rack, from Damage),
    /// their place in Profile_Test_Shipyard (which also gains the Cannon), and the Clipper's built-in loadout
    /// (<see cref="ClipperLoadout"/>). Run TinCan > Dev > Ship Designs > Build
    /// Assets first. Plan: .docs/plans/modular-airship-builder.md (S5).
    /// </summary>
    public static class ShipSocketsAssetBuilder
    {
        private const string InteractionPath = "Assets/Interactions/IA_MountFitting.asset";
        private const string ConfigPath = "Assets/Settings/ShipSockets/ShipSocketsConfig.asset";
        private const string StatePrefabPath = "Assets/Prefabs/ShipSockets/ShipSocketsState.prefab";
        private const string StateFixturePath = "Assets/Settings/Fixtures/ShipSocketsStateFixture.asset";
        private const string InstallerPath = "Assets/Resources/Installers/ShipSocketsFeatureInstaller.asset";
        private const string FittingsFolder = "Assets/Settings/ShipFittings/";
        private const string Installers = "Assets/Resources/Installers/";

        public const string CannonFittingId = "weapon.cannon";
        public const string ToolRackFittingId = "storage.tool_rack";
        public const string LeashWinchFittingId = "rescue.leash_winch";
        public const string ChartTableFittingId = "navigation.chart_table";
        public const string FuelReceiverFittingId = "fuel.receiver";
        public const string CrystalRackFittingId = "fuel.crystal_rack";
        public const string NetRackFittingId = "fuel.net_rack";

        public const string ClipperLoadoutPath = "Assets/Settings/ShipSockets/Clipper.fittings.json";
        public const string GalleonDesignKey = "Galleon";
        public const string GalleonLoadoutPath = "Assets/Settings/ShipSockets/Galleon.fittings.json";
        public const string GalleonPrefabPath = "Assets/Prefabs/Airship/Galleon_Prefab.prefab";
        public const string GalleonProfilePath = "Assets/Settings/FeatureProfiles/Profile_Galleon.asset";
        private const string SandboxProfilePath = "Assets/Settings/FeatureProfiles/Profile_FuelSandbox.asset";
        private const string GalleonScenePath = "Assets/Scenes/cheesed_scene.unity";
        private const string BaseProfilePath = "Assets/Settings/FeatureProfiles/Profile_Base.asset";
        private const string TestCoreProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Core.asset";
        public const string TestGalleonProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Galleon.asset";

        /// <summary>Sandbox installers the game leaves out: the voyage (the world is persistent) and the fuel minigame.</summary>
        private static readonly string[] NotInTheGame = { "VoyageFeatureInstaller", "FlyingCanFeatureInstaller" };

        /// <summary>Fixed-pose fixtures placed for Airship_Prefab's hull; on the Galleon they would float or sit in a part.</summary>
        private static readonly string[] OldHullFixtures =
        {
            "Assets/Settings/Fixtures/FuelSystemFixture.asset",
            "Assets/Settings/Fixtures/NetRackFixture.asset",
            "Assets/Settings/Fixtures/GrappleHookRackFixture.asset",
            "Assets/Settings/Fixtures/LeashWinchFixture.asset",
            "Assets/Settings/Fixtures/ChartTableFixture.asset",
            "Assets/Settings/Fixtures/ShipDamageSocketsFixture.asset",
        };

        [MenuItem("TinCan/Dev/Ship Sockets/Build Assets")]
        public static void Build()
        {
            var interaction = Asset<InteractionDefinition>(InteractionPath, definition =>
                SetField(definition, "_handlerTypeName", typeof(MountFittingInteractionHandler).AssemblyQualifiedName!));
            var config = Asset<ShipSocketsConfig>(ConfigPath, c => c.MountInteraction = interaction);
            var stateFixture = Asset<ShipFixtureDefinition>(StateFixturePath, fixture =>
            {
                fixture.Prefab = StatePrefab();
                fixture.LocalPosition = Vector3.zero;
                fixture.LocalEulerAngles = Vector3.zero;
            });
            var installer = Asset<ShipSocketsFeatureInstaller>(InstallerPath, i =>
            {
                SetField(i, "_config", config);
                SetField(i, "_stateFixture", stateFixture);
            });

            var cannon = Fitting("FITTING_CannonStation", CannonFittingId, "Cannon station", "Assets/Prefabs/Weapons/CannonStation.prefab");
            var rack = Fitting("FITTING_ToolRack", ToolRackFittingId, "Repair tool rack", "Assets/Prefabs/Airship/Parts/RepairToolRack.prefab");
            var cannonInstaller = Load<FeatureInstaller>(Installers + "CannonFeatureInstaller.asset");
            SetList(cannonInstaller, "_fittings", cannon);
            SetList(Load<FeatureInstaller>(Installers + "ShipDamageFeatureInstaller.asset"), "_fittings", rack);

            var profile = Load<FeatureProfile>(ShipDesignsAssetBuilder.ProfilePath);
            AddUnique(profile, "_installers", installer, cannonInstaller);

            // The Clipper's built-in loadout (its design is written by Ship Designs > Build Assets).
            AddUnique(config, nameof(ShipSocketsConfig.BuiltInLoadouts), WriteText(ClipperLoadoutPath, ShipFittingLoadoutJsonCodec.Encode(ClipperLoadout())));

            AssetDatabase.SaveAssets();
            Debug.Log("[ShipSocketsAssetBuilder] Ship socket assets built.");
        }

        /// <summary>
        /// The Clipper's stations for a crew of eight (<see cref="ShipFrameDesigns.ClipperMounts"/>): four cannons facing out
        /// over the sides, a pair on the quarterdeck and a pair in the waist; in the hold the fuel receiver (port aft) across
        /// from the crystal bin, the tool rack (port fore) across from the chart table; the anchor and the leash winch on the
        /// quarterdeck's stern corners, their chain and leash over the side; the hand crank on the cabin floor. With the helm,
        /// the same stations as the Galleon's.
        /// </summary>
        public static ShipFittingLoadout ClipperLoadout()
        {
            var mounts = ShipFrameDesigns.ClipperMounts();
            return new ShipFittingLoadout(new[]
            {
                new ShipFittingPlacement(new ShipSocketId(mounts.HoldPortAft, 0), FuelReceiverFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.HoldStarboardAft, 0), GatheringAssetBuilder.CrystalBinFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.HoldPortFore, 0), ToolRackFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.HoldStarboardFore, 0), ChartTableFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.QuarterdeckPort, 0), CannonFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.QuarterdeckStarboard, 0), CannonFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.WaistPort, 0), CannonFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.WaistStarboard, 0), CannonFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.SternPort, 0), AnchorAssetBuilder.FittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.SternStarboard, 0), LeashWinchFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.CabinFloor, 0), FuelAssetBuilder.CrankFittingId),
            });
        }

        /// <summary>
        /// TinCan > Dev > Ship Sockets > Build Galleon Airship: the Galleon's built-in loadout (four cannons, the fuel receiver and more),
        /// Galleon_Prefab (the modular ship with a preset: it flies the Galleon, and the fixed-pose fixtures made for the
        /// old hull stay off it), Profile_Galleon (the game's profile, <see cref="GameProfile"/>), both wired into
        /// cheesed_scene's scope, and Profile_Test_Galleon (the Test_Galleon area: the same game on the test range).
        /// Airship_Prefab is untouched. Run Build Assets first. Plans: .docs/plans/ship-fitting-loadouts.md,
        /// .docs/plans/peaceful-mode.md (P0).
        /// </summary>
        [MenuItem("TinCan/Dev/Ship Sockets/Build Galleon Airship")]
        public static void BuildGalleonAirship()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ShipSocketsAssetBuilder] Stop Play mode before building the Galleon airship.");
                return;
            }

            var loadout = WriteText(GalleonLoadoutPath, ShipFittingLoadoutJsonCodec.Encode(GalleonLoadout()));
            var config = Load<ShipSocketsConfig>(ConfigPath);
            AddUnique(config, nameof(ShipSocketsConfig.BuiltInLoadouts), loadout);

            GalleonPrefab();
            GameProfile();
            var test = Asset<FeatureProfile>(TestGalleonProfilePath, _ => { });
            AddUnique(test, "_includes", Load<FeatureProfile>(TestCoreProfilePath), Load<FeatureProfile>(GalleonProfilePath));
            AssetDatabase.SaveAssets();

            if (WireScene(GalleonScenePath, GalleonPrefabPath, GalleonProfilePath)) Debug.Log($"[ShipSocketsAssetBuilder] Galleon airship built; {GalleonScenePath} flies it.");
        }

        /// <summary>
        /// The game's profile, Profile_Galleon (cheesed_scene; plan: peaceful-mode.md, P0): the base, every installer the
        /// sandbox loads and the ones the Galleon adds, without the voyage and the flying-can minigame, and with
        /// Encounters, which keeps the event features quiet outside zones. Rebuilt from the sandbox on every run, so a
        /// feature a builder adds to the sandbox reaches the game the next time this runs. It no longer includes the
        /// sandbox: the voyage would come back with it.
        /// </summary>
        public static void GameProfile()
        {
            var encounters = Asset<TinCan.Features.Encounters.EncountersFeatureInstaller>(Installers + "EncountersFeatureInstaller.asset", _ => { });
            var sandbox = Load<FeatureProfile>(SandboxProfilePath);
            var game = Load<FeatureProfile>(GalleonProfilePath);
            var installers = sandbox.Installers
                .Concat(game.Installers)
                .Append(Load<FeatureInstaller>(Installers + "ShipDesignsFeatureInstaller.asset"))
                .Append(Load<FeatureInstaller>(InstallerPath))
                .Append(encounters)
                .Where(installer => installer != null && !NotInTheGame.Contains(installer.name))
                .Distinct()
                .Cast<Object>()
                .ToArray();

            SetList(game, "_includes", Load<FeatureProfile>(BaseProfilePath));
            SetList(game, "_installers", installers);
        }

        /// <summary>
        /// The Galleon's mount plates (Galleon.ship.json, <see cref="ShipFrameDesigns.GalleonMounts"/>): cannons at the bow and
        /// the stern, the tool rack in the port forward hold, the leash winch (Respawn) on the starboard balcony, the chart
        /// table (Navigation) in the starboard forward hold, the fuel receiver (Fuel) in the port aft hold with the crystal
        /// bin (Gathering) across from it in the starboard aft hold, and the anchor (Anchor) on the quarterdeck's port stern
        /// corner, by the helm, and the hand crank (Fuel) on the cabin floor's plate, above the engines. The crystal rack and
        /// the net rack (the fuel minigame) left the game (peaceful-mode.md, P0). The socket ids are part instance ids in the committed design.
        /// </summary>
        public static ShipFittingLoadout GalleonLoadout()
        {
            var mounts = ShipFrameDesigns.GalleonMounts();
            return new ShipFittingLoadout(new[]
            {
                new ShipFittingPlacement(new ShipSocketId(mounts.HoldPortAft, 0), FuelReceiverFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.QuarterdeckPortAft, 0), AnchorAssetBuilder.FittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.HoldPortFore, 0), ToolRackFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.BowPort, 0), CannonFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.BowStarboard, 0), CannonFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.SternPort, 0), CannonFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.SternStarboard, 0), CannonFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.Balcony, 0), LeashWinchFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.HoldStarboardFore, 0), ChartTableFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.HoldStarboardAft, 0), GatheringAssetBuilder.CrystalBinFittingId),
                new ShipFittingPlacement(new ShipSocketId(mounts.CabinFloor, 0), FuelAssetBuilder.CrankFittingId),
            });
        }

        /// <summary>A variant of the modular ship (no geometry; the design builds it) whose preset names the Galleon.</summary>
        private static GameObject GalleonPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GalleonPrefabPath);
            GameObject root;
            if (existing != null)
            {
                root = PrefabUtility.LoadPrefabContents(GalleonPrefabPath);
            }
            else
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ShipDesignsAssetBuilder.ModularShipPath));
                root.name = "Galleon_Prefab";
            }

            try
            {
                var preset = root.GetComponent<ShipDesignPreset>();
                if (preset == null) preset = root.AddComponent<ShipDesignPreset>();
                preset.Configure(GalleonDesignKey, OldHullFixtures.Select(Load<ShipFixtureDefinition>));
                EditorUtility.SetDirty(preset);
                PrefabUtility.SaveAsPrefabAsset(root, GalleonPrefabPath);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }

            var prefab = Load<GameObject>(GalleonPrefabPath);
            if (existing == null) Rehash(prefab);
            return prefab;
        }

        /// <summary>Sets a scene's scope to fly the given ship with the given profile, then reopens the scene that was open.</summary>
        private static bool WireScene(string scenePath, string shipPath, string profilePath)
        {
            var active = SceneManager.GetActiveScene();
            if (active.isDirty)
            {
                Debug.LogError($"[ShipSocketsAssetBuilder] '{active.name}' has unsaved changes; save it, then build again to wire {scenePath}.");
                return false;
            }

            string returnTo = active.path;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var scope = scene.GetRootGameObjects().Select(o => o.GetComponent("ProjectLifetimeScope")).FirstOrDefault(c => c != null)
                        ?? throw new InvalidOperationException($"{scenePath} has no ProjectLifetimeScope at its root.");
            // Load assets only after OpenScene: opening a scene unloads unused assets, so an earlier reference is dead.
            var fields = new SerializedObject(scope);
            fields.FindProperty("_airshipPrefab").objectReferenceValue = Load<GameObject>(shipPath);
            fields.FindProperty("_featureProfile").objectReferenceValue = Load<FeatureProfile>(profilePath);
            fields.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(returnTo) && returnTo != scenePath) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
            return true;
        }

        private static void Rehash(GameObject prefab)
        {
            var networkObject = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            EditorUtility.SetDirty(prefab);
        }

        private static TextAsset WriteText(string path, string text)
        {
            EnsureFolder(path);
            File.WriteAllText(Path.GetFullPath(path), text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return Load<TextAsset>(path);
        }

        private static ShipFittingDefinition Fitting(string asset, string fittingId, string displayName, string prefabPath) =>
            Asset<ShipFittingDefinition>(FittingsFolder + asset + ".asset", fitting =>
            {
                var fields = new SerializedObject(fitting);
                fields.FindProperty("_fittingId").stringValue = fittingId;
                fields.FindProperty("_displayName").stringValue = displayName;
                fields.FindProperty("_prefab").objectReferenceValue = Load<GameObject>(prefabPath);
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

        /// <summary>The ShipSocketsState fixture: a NetworkObject carrying what is mounted in a ship's sockets.</summary>
        private static GameObject StatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatePrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("ShipSocketsState");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            root.AddComponent<ShipSocketsNetworkMediator>();

            EnsureFolder(StatePrefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StatePrefabPath);
            Object.DestroyImmediate(root);

            // A new NetworkObject prefab keeps a stale id until validated; a client would refuse the host.
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
