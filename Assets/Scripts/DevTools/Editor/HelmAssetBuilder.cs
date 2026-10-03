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
using TinCan.Features.Helm;
using TinCan.Features.Stations;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Helm > Build Assets: creates (or re-tunes) every asset of the helm station, so its data is
    /// reviewable as code: the occupying tag, effect and ability, the interaction, the helm prefab and its fixture, the
    /// installer, and the profiles that load it. Scriptable objects are re-applied on every run; the prefab is built only
    /// when missing (delete it to rebuild), so its network id stays stable. Run TinCan > Dev > Input > Build Assets
    /// afterwards: it builds Context_Helmsman (which needs the tag) and points the installer at it.
    /// Plan: .docs/plans/helm-station.md.
    /// </summary>
    public static class HelmAssetBuilder
    {
        private const string Tags = "Assets/Abilities/Tags/";
        private const string Effects = "Assets/Abilities/Effects/";
        private const string Abilities = "Assets/Abilities/AbilityDefinitions/";
        private const string Attributes = "Assets/Abilities/Attributes/";
        private const string HelmPrefab = "Assets/Prefabs/Airship/Parts/HelmStation.prefab";
        private const string Installers = "Assets/Resources/Installers/";

        // At the ship's wheel (StylShip_Wheel, x 1.06, z -8.12), on the wheel stand's base (deck y 1.087 in ship space,
        // raycast 2026-10-03). The test ship has a quarterdeck at the same height for it. The seat is behind the wheel,
        // facing the bow, still on the stand base (it reaches z -9.3).
        private static readonly Vector3 HelmLocalPosition = new(1.05f, 1.087f, -8.1f);
        private static readonly Vector3 SeatLocalPosition = new(0f, 1.05f, -1.2f);

        [MenuItem("TinCan/Dev/Helm/Build Assets")]
        public static void Build()
        {
            // Tags
            var occupying = Tag("State.Occupying");
            var occupyingHelm = Tag("State.Occupying.Helm", occupying);
            var occupyTag = Tag("Ability.Occupy.Helm");
            Load<GameplayTagDatabase>("Assets/Abilities/GameplayTagDatabase.asset").Refresh();

            // The helmsman stands still at the wheel: walking and jumping are off while the tag is held.
            var moveSpeed = Load<GameplayAttribute>(Attributes + "Attr_MoveSpeed.asset");
            var jumpForce = Load<GameplayAttribute>(Attributes + "Attr_JumpForce.asset");
            var occupyingEffect = Asset<GameplayEffectDefinition>(Effects + "GE_Occupying_Helm.asset", effect =>
            {
                effect.DurationType = DurationType.Infinite;
                effect.GrantedTags = new List<GameplayTag> { occupyingHelm };
                effect.Modifiers = new List<AttributeModifier>
                {
                    new() { Attribute = moveSpeed, Operation = ModifierOp.Override, Value = 0f },
                    new() { Attribute = jumpForce, Operation = ModifierOp.Override, Value = 0f }
                };
            });
            var occupyAbility = Asset<AbilityDefinition>(Abilities + "GA_OccupyHelm.asset", ability =>
            {
                Reset(ability);
                ability.AbilityTag = occupyTag;
                ability.ActiveEffect = occupyingEffect;
                ability.ActiveEffectTarget = EffectTarget.Self;
                ability.ActivationBlockedTagsOnActor.Add(occupying);
            });
            var takeHelm = Asset<InteractionDefinition>("Assets/Interactions/IA_TakeHelm.asset", definition =>
                SetField(definition, "_handlerTypeName", typeof(OccupyStationInteractionHandler).AssemblyQualifiedName!));

            // Prefab and fixture
            var helm = BuildHelmPrefab(takeHelm, occupyAbility);
            var fixture = Asset<ShipFixtureDefinition>("Assets/Settings/Fixtures/HelmStationFixture.asset", definition =>
            {
                definition.Prefab = helm;
                definition.LocalPosition = HelmLocalPosition;
                definition.LocalEulerAngles = Vector3.zero;
            });

            // Installer and profiles: the main game and the helm's test area (Stations comes with the cannon's installers
            // in the main game; the test area adds it).
            var stations = Load<StationsFeatureInstaller>(Installers + "StationsFeatureInstaller.asset");
            var installer = Asset<HelmFeatureInstaller>(Installers + "HelmFeatureInstaller.asset", asset =>
                SetList(asset, "_helmFixtures", new Object[] { fixture }));

            AddToProfile(Load<FeatureProfile>("Assets/Settings/FeatureProfiles/Profile_FuelSandbox.asset"), new Object[] { stations, installer });
            var testProfile = Asset<FeatureProfile>("Assets/Settings/FeatureProfiles/Test/Profile_Test_Helm.asset", _ => { });
            AddInclude(testProfile, Load<FeatureProfile>("Assets/Settings/FeatureProfiles/Test/Profile_Test_Core.asset"));
            AddToProfile(testProfile, new Object[] { stations, installer });

            AssetDatabase.SaveAssets();
            Debug.Log("[HelmAssetBuilder] Helm assets are up to date. Run TinCan > Dev > Input > Build Assets for Context_Helmsman.");
        }

        private static GameObject BuildHelmPrefab(InteractionDefinition interaction, AbilityDefinition occupy)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HelmPrefab);
            if (existing != null) return existing;

            var root = new GameObject("HelmStation");
            root.AddComponent<NetworkObject>();
            root.AddComponent<EntityNetworkMediator>();
            var mediator = root.AddComponent<HelmNetworkMediator>();
            SetField(mediator, "_interactionDefinition", interaction);
            SetField(mediator, "_occupyAbility", occupy);

            // No visuals: the ship model has the wheel. This box around it is what Interact aims at.
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.75f, 0f);
            box.size = new Vector3(2f, 2f, 0.8f);

            var seat = new GameObject("Seat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = SeatLocalPosition;

            EnsureFolder(HelmPrefab);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HelmPrefab);
            Object.DestroyImmediate(root);

            // The NetworkObject's id was hashed while it was a scene object; its validation re-hashes it from the asset.
            var networkObject = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            return prefab;
        }

        private static GameplayTag Tag(string name, GameplayTag? parent = null) =>
            Asset<GameplayTag>(Tags + name + ".asset", tag => SetField(tag, "_parent", parent));

        private static void Reset(AbilityDefinition ability)
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

        private static void SetField(Object target, string field, object? value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            if (property.propertyType == SerializedPropertyType.String) property.stringValue = (string)value!;
            else property.objectReferenceValue = (Object?)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetList(Object target, string field, IReadOnlyList<Object> values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToProfile(FeatureProfile profile, IEnumerable<Object> installers) => AddUnique(profile, "_installers", installers);

        private static void AddInclude(FeatureProfile profile, FeatureProfile include) => AddUnique(profile, "_includes", new Object[] { include });

        private static void AddUnique(Object target, string field, IEnumerable<Object> values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
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
