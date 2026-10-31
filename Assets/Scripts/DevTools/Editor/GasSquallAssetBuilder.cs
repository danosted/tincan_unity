#nullable enable
using System;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Features.Airship.Damage;
using TinCan.Features.DesignedEvents;
using TinCan.Features.GasChallenge;
using TinCan.Features.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Gas Squall > Build Assets: creates (or re-applies) the assets of the gas squall plan, so its data is
    /// reviewable as code:
    /// <list type="bullet">
    /// <item>the EventState prefab and its ship fixture, on the events installer (event state every peer sees);</item>
    /// <item>the single ShipDamagePoint prefab (made from a ShipDamageSockets socket), on the ship damage config;</item>
    /// <item>GasPocket_Networked (a variant of the hand-placed GasPocket) and the GasChallengeConfig, on the gas installer;</item>
    /// <item>the chart's gas material, the explosion's hull damage, and the profiles that load the gas challenge.</item>
    /// </list>
    /// Prefabs are built only when missing (delete one to rebuild it), so their network ids stay stable. Plan:
    /// .docs/plans/gas-squall-event.md.
    /// </summary>
    public static class GasSquallAssetBuilder
    {
        private const string Installers = "Assets/Resources/Installers/";
        private const string Profiles = "Assets/Settings/FeatureProfiles/";
        private const string EventStatePrefab = "Assets/Prefabs/Events/EventState.prefab";
        private const string DamagePointPrefab = "Assets/Prefabs/Airship/Parts/ShipDamagePoint.prefab";
        private const string DamageSocketsPrefab = "Assets/Prefabs/Airship/Parts/ShipDamageSockets.prefab";
        private const string PocketPrefab = "Assets/Prefabs/Hazards/GasPocket.prefab";
        private const string NetworkedPocketPrefab = "Assets/Prefabs/Hazards/GasPocket_Networked.prefab";

        // Hull health one pocket takes (it was -350, which sank a small ship outright; the breaks are now the cost).
        private const float ExplosionHealth = -150f;

        [MenuItem("TinCan/Dev/Gas Squall/Build Assets")]
        public static void Build()
        {
            var stateFixture = Asset<ShipFixtureDefinition>("Assets/Settings/Fixtures/EventStateFixture.asset", definition =>
            {
                definition.Prefab = Prefab(EventStatePrefab, _ =>
                {
                    var root = new GameObject("EventState");
                    root.AddComponent<NetworkObject>();
                    root.AddComponent<EntityNetworkMediator>();
                    root.AddComponent<EventStateNetworkMediator>();
                    return root;
                });
                definition.LocalPosition = Vector3.zero;
                definition.LocalEulerAngles = Vector3.zero;
            });
            SetField(Load<EventsFeatureInstaller>(Installers + "EventsFeatureInstaller.asset"), "_stateFixture", stateFixture);

            var damage = Load<ShipDamageConfig>("Assets/Settings/ShipDamageConfig.asset");
            damage.DesignedShipPointPrefab = Prefab(DamagePointPrefab, SinglePoint);
            EditorUtility.SetDirty(damage);

            var gas = Asset<GasChallengeConfig>("Assets/Settings/GasChallenge/GasChallengeConfig.asset", config =>
                config.PocketPrefab = Prefab(NetworkedPocketPrefab, NetworkedPocket));
            var gasInstaller = Load<GasChallengeFeatureInstaller>(Installers + "GasChallengeFeatureInstaller.asset");
            SetField(gasInstaller, "_config", gas);

            var navigation = Load<NavigationConfig>("Assets/Settings/Navigation/NavigationConfig.asset");
            navigation.GasMaterial = ChartMaterial("Assets/Materials/Navigation/M_Chart_Gas.mat", new Color(0.45f, 0.85f, 0.3f));
            EditorUtility.SetDirty(navigation);

            SetExplosionHealth(Load<Object>("Assets/Abilities/Effects/GE_GasPocketExplosion.asset"));

            // GasChallenge builds on events and ship damage, so it leaves the base profiles for the ones that load both.
            Remove(Load<FeatureProfile>(Profiles + "Profile_Base.asset"), gasInstaller);
            Remove(Load<FeatureProfile>(Profiles + "Test/Profile_Test_Core.asset"), gasInstaller);
            Add(Load<FeatureProfile>(Profiles + "Profile_FuelSandbox.asset"), gasInstaller);
            Add(Load<FeatureProfile>(Profiles + "Test/Profile_Test_ShipDamage.asset"), gasInstaller);

            AssetDatabase.SaveAssets();
            Debug.Log("[GasSquallAssetBuilder] Gas squall assets built.");
        }

        /// <summary>One socket of the damage-sockets fixture as its own networked object: its mediators, marker and collider.</summary>
        private static GameObject SinglePoint(Scene scene)
        {
            var sockets = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(DamageSocketsPrefab), scene);
            PrefabUtility.UnpackPrefabInstance(sockets, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var point = sockets.transform.Find("Socket_0")?.gameObject
                        ?? throw new InvalidOperationException($"{DamageSocketsPrefab} has no Socket_0.");

            point.transform.SetParent(null, false);
            point.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            point.name = "ShipDamagePoint";
            Object.DestroyImmediate(sockets);

            point.AddComponent<NetworkObject>();
            point.AddComponent<EntityNetworkMediator>();
            return point;
        }

        /// <summary>A variant of the hand-placed pocket, networked: its detonation replicates (GasPocketNetworkMediator).</summary>
        private static GameObject NetworkedPocket(Scene scene)
        {
            var pocket = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PocketPrefab), scene);
            pocket.name = "GasPocket_Networked";
            pocket.AddComponent<NetworkObject>();
            pocket.AddComponent<EntityNetworkMediator>();
            pocket.AddComponent<GasPocketNetworkMediator>();
            return pocket;
        }

        private static void SetExplosionHealth(Object effect)
        {
            var serialized = new SerializedObject(effect);
            var modifiers = serialized.FindProperty("Modifiers");
            if (modifiers == null || modifiers.arraySize == 0) throw new InvalidOperationException($"{effect.name} has no modifier.");

            modifiers.GetArrayElementAtIndex(0).FindPropertyRelative("Value").floatValue = ExplosionHealth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The prefab at <paramref name="path"/>, built from <paramref name="build"/> only when missing.</summary>
        private static GameObject Prefab(string path, Func<Scene, GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            // Built in a preview scene, so the open scene is not marked modified.
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject prefab;
            try
            {
                var root = build(preview);
                if (root.scene != preview) SceneManager.MoveGameObjectToScene(root, preview);
                EnsureFolder(path);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            // The NetworkObject's id was hashed while it was a scene object; its validation re-hashes it from the asset.
            var networkObject = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            return prefab;
        }

        /// <summary>A copy of the chart's hazard material in another colour, so it uses the same shader.</summary>
        private static Material ChartMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Load<Material>("Assets/Materials/Navigation/M_Chart_Hazard.mat")) { name = Path.GetFileNameWithoutExtension(path) };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Loads the asset at <paramref name="path"/> or creates it, applies <paramref name="configure"/>, and marks it dirty.</summary>
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
            EditorUtility.SetDirty(target);
        }

        private static void Add(FeatureProfile profile, Object installer)
        {
            var serialized = new SerializedObject(profile);
            var installers = serialized.FindProperty("_installers");
            for (int i = 0; i < installers.arraySize; i++)
            {
                if (installers.GetArrayElementAtIndex(i).objectReferenceValue == installer) return;
            }

            installers.arraySize++;
            installers.GetArrayElementAtIndex(installers.arraySize - 1).objectReferenceValue = installer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void Remove(FeatureProfile profile, Object installer)
        {
            var serialized = new SerializedObject(profile);
            var installers = serialized.FindProperty("_installers");
            for (int i = installers.arraySize - 1; i >= 0; i--)
            {
                if (installers.GetArrayElementAtIndex(i).objectReferenceValue != installer) continue;
                installers.GetArrayElementAtIndex(i).objectReferenceValue = null;
                installers.DeleteArrayElementAtIndex(i);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
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
