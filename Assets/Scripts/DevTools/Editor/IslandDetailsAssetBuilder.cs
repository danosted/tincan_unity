#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Interaction;
using TinCan.DevTools.Scenarios;
using TinCan.Features.Camping;
using TinCan.Features.IslandDetails;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Island Details > Build Assets: the trees (three autumn colours; networked deadwood piles whose canopy
    /// lobes are the logs: IA_GatherFirewood and CampingConfig, so run TinCan > Dev > Camping > Build Assets first), the
    /// boulders and bushes, the config and installer, the installer's place in Profile_Galleon (the live game), and the
    /// Test_Islands area (its profile: sky islands, camping and island details on the test ship; and its
    /// scene). Scriptable objects are re-applied on every run; prefabs and the scene are built only when missing (delete
    /// one to rebuild it), so network ids stay stable. The models are placeholders made of primitives in the look's
    /// library materials; please review them in play. Plan: .docs/plans/island-life.md.
    /// </summary>
    public static class IslandDetailsAssetBuilder
    {
        private const string Prefabs = "Assets/Prefabs/IslandDetails/";
        private const string ConfigPath = "Assets/Settings/IslandDetails/IslandDetailsConfig.asset";
        private const string InstallerPath = "Assets/Resources/Installers/IslandDetailsFeatureInstaller.asset";
        private const string Installers = "Assets/Resources/Installers/";
        private const string TestProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Islands.asset";
        private const string TestCoreProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Core.asset";
        private const string CampingConfigPath = "Assets/Settings/Camping/CampingConfig.asset";
        private const string GatherPath = "Assets/Interactions/IA_GatherFirewood.asset";

        /// <summary>The name of the tree's part that holds its firewood (the interaction target), for scenarios.</summary>
        public const string TreeTargetName = "Branches";

        [MenuItem("TinCan/Dev/Island Details/Build Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[IslandDetailsAssetBuilder] Stop Play mode before building the island details assets.");
                return;
            }

            var camping = Load<CampingConfig>(CampingConfigPath);
            var gather = Load<InteractionDefinition>(GatherPath);
            var m = new Materials();

            var trees = new List<GameObject>
            {
                TreePrefab("IslandTree_Russet", camping, gather, m, m.Russet, m.Orange, m.Gold),
                TreePrefab("IslandTree_Orange", camping, gather, m, m.Orange, m.Gold, m.Russet),
                TreePrefab("IslandTree_Gold", camping, gather, m, m.Gold, m.Orange, m.Gold)
            };
            var boulders = new List<GameObject> { Scenery("Boulder_Round", root => RoundBoulder(root, m)), Scenery("Boulder_Standing", root => StandingStone(root, m)) };
            var bushes = new List<GameObject>
            {
                Scenery("Bush_Russet", root => Bush(root, m.Russet, m.Orange)),
                Scenery("Bush_Gold", root => Bush(root, m.Gold, m.Orange))
            };

            var config = Asset<IslandDetailsConfig>(ConfigPath, c =>
            {
                c.TreePrefabs = trees;
                c.BoulderPrefabs = boulders;
                c.BushPrefabs = bushes;
            });
            var installer = Asset<IslandDetailsFeatureInstaller>(InstallerPath, i => SetField(i, "_config", config));

            var galleon = Load<FeatureProfile>(ShipSocketsAssetBuilder.GalleonProfilePath);
            AddUnique(galleon, "_installers", installer);
            var testProfile = Asset<FeatureProfile>(TestProfilePath, _ => { });
            AddUnique(testProfile, "_includes", Load<FeatureProfile>(TestCoreProfilePath));
            AddUnique(testProfile, "_installers",
                Load<Object>(Installers + "SkyIslandsFeatureInstaller.asset"),
                Load<Object>(Installers + "CampingFeatureInstaller.asset"),
                installer);
            // Island camps (pre-placed campsites) are gone: players place their own fires and tents.
            RemoveMissing(galleon, "_installers");
            RemoveMissing(testProfile, "_installers");

            AssetDatabase.SaveAssets();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenes.Islands) == null) TestRangeSceneBuilder.BuildArea(TestScenes.Islands);
            TestRangeSceneBuilder.SyncBuildList();
            Debug.Log("[IslandDetailsAssetBuilder] Island details assets are up to date.");
        }

        private sealed class Materials
        {
            public readonly Material Bark = LookMaterials.Get("M_ShipWalls");
            public readonly Material Stone = LookMaterials.Get("M_ShipCairn");
            public readonly Material Moss = LookMaterials.Get("M_IslandMoss");
            public readonly Material Russet = LookMaterials.Get("M_FoliageRusset");
            public readonly Material Orange = LookMaterials.Get("M_FoliageOrange");
            public readonly Material Gold = LookMaterials.Get("M_FoliageGold");
        }

        /// <summary>
        /// An autumn tree, its root on the ground (y 0): a trunk with a flared foot and two bare boughs, and a canopy of
        /// three lobes, which are its firewood (Log_0..2: the last taken first, the tree left bare until they grow back).
        /// The trunk is solid; a trigger round it at chest height is where a player gathers.
        /// </summary>
        private static GameObject TreePrefab(string name, CampingConfig camping, InteractionDefinition gather, Materials m,
            Material main, Material second, Material highlight)
        {
            string path = Prefabs + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = NetworkedRoot(name);
            var branches = new GameObject(TreeTargetName).transform;
            branches.SetParent(root.transform, false);
            var pile = branches.gameObject.AddComponent<DeadwoodNetworkMediator>();
            SetField(pile, "_interactionDefinition", gather);
            SetField(pile, "_config", camping);

            Visual(PrimitiveType.Sphere, "Flare", branches, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(1.1f, 0.5f, 1.1f), m.Bark);
            Visual(PrimitiveType.Cylinder, "Trunk", branches, new Vector3(0f, 1.9f, 0f), new Vector3(0f, 0f, 2f), new Vector3(0.55f, 2.4f, 0.55f), m.Bark);
            Visual(PrimitiveType.Cylinder, "Bough_0", branches, new Vector3(0.55f, 3.7f, 0.1f), new Vector3(0f, 0f, -42f), new Vector3(0.2f, 0.9f, 0.2f), m.Bark);
            Visual(PrimitiveType.Cylinder, "Bough_1", branches, new Vector3(-0.45f, 3.4f, -0.25f), new Vector3(12f, 0f, 36f), new Vector3(0.17f, 0.8f, 0.17f), m.Bark);
            Visual(PrimitiveType.Sphere, "Log_0", branches, new Vector3(0f, 5.1f, 0f), new Vector3(0f, 20f, 0f), new Vector3(3.8f, 3.0f, 3.6f), main);
            Visual(PrimitiveType.Sphere, "Log_1", branches, new Vector3(1.15f, 4.35f, 0.55f), Vector3.zero, new Vector3(2.6f, 2.2f, 2.6f), second);
            Visual(PrimitiveType.Sphere, "Log_2", branches, new Vector3(-1.05f, 4.55f, -0.6f), Vector3.zero, new Vector3(2.4f, 2.0f, 2.3f), highlight);

            var trunk = branches.gameObject.AddComponent<CapsuleCollider>();
            trunk.center = new Vector3(0f, 2f, 0f);
            trunk.radius = 0.35f;
            trunk.height = 4.6f;
            trunk.direction = 1;
            InteractVolume(branches, new Vector3(0f, 1.2f, 0f), new Vector3(1.4f, 2.4f, 1.4f));

            return SaveNetworkPrefab(root, path);
        }

        /// <summary>A low rounded boulder of two stones with moss on top. Solid.</summary>
        private static void RoundBoulder(Transform root, Materials m)
        {
            Visual(PrimitiveType.Sphere, "Stone", root, new Vector3(0f, 0.35f, 0f), new Vector3(0f, 15f, 8f), new Vector3(1.6f, 1.0f, 1.3f), m.Stone);
            Visual(PrimitiveType.Sphere, "Stone_Small", root, new Vector3(0.65f, 0.22f, 0.35f), new Vector3(0f, -20f, 0f), new Vector3(0.9f, 0.65f, 0.8f), m.Stone);
            Visual(PrimitiveType.Sphere, "Moss", root, new Vector3(-0.05f, 0.78f, -0.05f), new Vector3(0f, 15f, 8f), new Vector3(1.05f, 0.28f, 0.85f), m.Moss);
            var collider = root.gameObject.AddComponent<SphereCollider>();
            collider.center = new Vector3(0f, 0.35f, 0f);
            collider.radius = 0.7f;
        }

        /// <summary>A leaning standing stone with a smaller one at its foot. Solid.</summary>
        private static void StandingStone(Transform root, Materials m)
        {
            Visual(PrimitiveType.Sphere, "Stone", root, new Vector3(0f, 0.85f, 0f), new Vector3(6f, 0f, -5f), new Vector3(0.95f, 1.9f, 0.85f), m.Stone);
            Visual(PrimitiveType.Sphere, "Stone_Foot", root, new Vector3(-0.5f, 0.2f, 0.35f), Vector3.zero, new Vector3(0.75f, 0.5f, 0.65f), m.Stone);
            Visual(PrimitiveType.Sphere, "Moss", root, new Vector3(0.04f, 1.68f, 0f), new Vector3(6f, 0f, -5f), new Vector3(0.6f, 0.3f, 0.55f), m.Moss);
            var collider = root.gameObject.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.85f, 0f);
            collider.radius = 0.45f;
            collider.height = 1.9f;
            collider.direction = 1;
        }

        /// <summary>A round bush of three lobes. Not solid.</summary>
        private static void Bush(Transform root, Material main, Material second)
        {
            Visual(PrimitiveType.Sphere, "Lobe_0", root, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(1.3f, 0.95f, 1.3f), main);
            Visual(PrimitiveType.Sphere, "Lobe_1", root, new Vector3(0.5f, 0.32f, 0.3f), Vector3.zero, new Vector3(0.9f, 0.7f, 0.9f), second);
            Visual(PrimitiveType.Sphere, "Lobe_2", root, new Vector3(-0.45f, 0.3f, -0.2f), Vector3.zero, new Vector3(0.8f, 0.6f, 0.8f), main);
        }

        private static GameObject Scenery(string name, Action<Transform> model)
        {
            string path = Prefabs + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject(name);
            model(root.transform);
            EnsureFolder(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void InteractVolume(Transform parent, Vector3 centre, Vector3 size)
        {
            var volume = new GameObject("InteractVolume").AddComponent<BoxCollider>();
            volume.transform.SetParent(parent, false);
            volume.transform.localPosition = centre;
            volume.isTrigger = true;
            volume.size = size;
        }

        private static GameObject NetworkedRoot(string name)
        {
            var root = new GameObject(name);
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = false;
            root.AddComponent<EntityNetworkMediator>();
            return root;
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
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException(
                $"Missing asset {path}. Build the camping and sky island assets first (TinCan > Dev > Camping / Sky Islands > Build Assets).");

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

        /// <summary>Drops the entries whose asset was deleted (they read as missing references).</summary>
        private static void RemoveMissing(Object target, string field)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            for (int i = property.arraySize - 1; i >= 0; i--)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == null) property.DeleteArrayElementAtIndex(i);
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
