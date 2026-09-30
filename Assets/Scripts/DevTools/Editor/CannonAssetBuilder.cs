#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using TinCan.Core.Gas;
using TinCan.Core.Entities;
using TinCan.Core.Interaction;
using TinCan.Features.SkyHazards;
using TinCan.Features.Stations;
using TinCan.Core.Targeting;
using TinCan.Features.Weapons.Cannon;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Cannon > Build Assets: creates (or re-tunes) every asset of the cannon and sky-hazard slice, so the
    /// slice's data is reviewable as code: tags, effects, abilities, the sweep and interaction definitions, configs, the
    /// cannon and hazard prefabs, the fixture, the installers, and the profiles that load them. Scriptable objects are
    /// re-applied on every run; prefabs are built only when missing (delete one to rebuild it), so their network ids stay
    /// stable. Plan: .docs/plans/cannon-and-hazards.md.
    /// </summary>
    public static class CannonAssetBuilder
    {
        private const string Tags = "Assets/Abilities/Tags/";
        private const string Effects = "Assets/Abilities/Effects/";
        private const string Abilities = "Assets/Abilities/AbilityDefinitions/";
        private const string Attributes = "Assets/Abilities/Attributes/";
        private const string Materials = "Assets/Materials/Weapons/";
        private const string CannonPrefab = "Assets/Prefabs/Weapons/CannonStation.prefab";
        private const string HazardPrefab = "Assets/Prefabs/SkyHazards/SkyHazard.prefab";
        private const string Installers = "Assets/Resources/Installers/";
        private const string AbilityMediatorType = "TinCan.Network.Infrastructure.Abilities.AbilityNetworkMediator, TinCan.Network";
        private const string TransformMediatorType = "TinCan.Network.Infrastructure.NetworkTransformMediator, TinCan.Network";

        // A pair of broadsides on the foredeck (y = -1.815 in ship space on the airship's FloorFront and the test ship's
        // Foredeck), mirrored about the centreline x = 1. At z 17 the deck is clear between the front mast (ends z 16.2)
        // and the bow fencing, for x -2..4 (overlap-checked against the airship, 2026-09-30); the seats face inboard.
        // Hazards come from ahead, so CannonConfig.YawLimit lets each barrel swing past 90 deg across the bow.
        private static readonly Vector3 StarboardCannonLocalPosition = new(3.9f, -1.815f, 17f);
        private static readonly Vector3 StarboardCannonLocalEuler = new(0f, 90f, 0f);
        private static readonly Vector3 PortCannonLocalPosition = new(-1.9f, -1.815f, 17f);
        private static readonly Vector3 PortCannonLocalEuler = new(0f, -90f, 0f);

        [MenuItem("TinCan/Dev/Cannon/Build Assets")]
        public static void Build()
        {
            // Tags
            var occupying = Tag("State.Occupying");
            var occupyingCannon = Tag("State.Occupying.Cannon", occupying);
            var reloading = Tag("State.Cannon.Reloading");
            var fireTag = Tag("Ability.Cannon.Fire");
            var occupyTag = Tag("Ability.Occupy.Cannon");

            // Effects
            var health = Load<GameplayAttribute>(Attributes + "Attr_Health.asset");
            var maxHealth = Load<GameplayAttribute>(Attributes + "Attr_MaxHealth.asset");
            var moveSpeed = Load<GameplayAttribute>(Attributes + "Attr_MoveSpeed.asset");
            var jumpForce = Load<GameplayAttribute>(Attributes + "Attr_JumpForce.asset");

            var occupyingEffect = Asset<GameplayEffectDefinition>(Effects + "GE_Occupying_Cannon.asset", effect =>
            {
                effect.DurationType = DurationType.Infinite;
                effect.GrantedTags = new List<GameplayTag> { occupyingCannon };
                effect.Modifiers = new List<AttributeModifier>
                {
                    new() { Attribute = moveSpeed, Operation = ModifierOp.Override, Value = 0f },
                    new() { Attribute = jumpForce, Operation = ModifierOp.Override, Value = 0f }
                };
            });
            var reload = Asset<GameplayEffectDefinition>(Effects + "GE_CannonReload.asset", effect =>
            {
                effect.DurationType = DurationType.Duration;
                effect.DurationSeconds = 1.5f;
                effect.GrantedTags = new List<GameplayTag> { reloading };
                effect.Modifiers = new List<AttributeModifier>();
            });
            var hit = Asset<GameplayEffectDefinition>(Effects + "GE_CannonballHit.asset", effect =>
            {
                effect.DurationType = DurationType.Instant;
                effect.GrantedTags = new List<GameplayTag>();
                effect.Modifiers = new List<AttributeModifier>
                {
                    new() { Attribute = health, Operation = ModifierOp.Add, Value = -100f, ClampMaxAttribute = maxHealth }
                };
            });

            var impact = Asset<GameplayEffectDefinition>(Effects + "GE_HazardImpact.asset", effect =>
            {
                effect.DurationType = DurationType.Instant;
                effect.GrantedTags = new List<GameplayTag>();
                effect.Modifiers = new List<AttributeModifier>
                {
                    new() { Attribute = health, Operation = ModifierOp.Add, Value = -50f, ClampMaxAttribute = maxHealth }
                };
            });

            // Abilities
            var occupyAbility = Asset<AbilityDefinition>(Abilities + "GA_OccupyCannon.asset", ability =>
            {
                Reset(ability);
                ability.AbilityTag = occupyTag;
                ability.ActiveEffect = occupyingEffect;
                ability.ActiveEffectTarget = EffectTarget.Self;
                ability.ActivationBlockedTagsOnActor.Add(occupying);
            });
            var fireAbility = Asset<AbilityDefinition>(Abilities + "GA_FireCannon.asset", ability =>
            {
                Reset(ability);
                ability.AbilityTag = fireTag;
                ability.EndsImmediately = true;
                ability.CooldownEffect = reload;
                ability.ActivationRequiredTagsOnActor.Add(occupyingCannon);
            });

            // Queries and interactions
            var sweep = Asset<TargetingDefinition>("Assets/Targeting/TD_CannonballSweep.asset", definition =>
            {
                definition.Shape = TargetShape.Ray;
                definition.Radius = 0.3f;
                definition.Selection = TargetSelection.FirstHit;
            });
            var occupyInteraction = Asset<InteractionDefinition>("Assets/Interactions/IA_OccupyCannon.asset", definition =>
                SetField(definition, "_handlerTypeName", typeof(OccupyStationInteractionHandler).AssemblyQualifiedName!));

            // Configs
            var cannonConfig = Asset<CannonConfig>("Assets/Settings/Cannon/CannonConfig.asset", config =>
            {
                config.FireInput = Load<GameplayInput>("Assets/Abilities/Inputs/Input_Primary.asset");
                config.FireAbility = fireAbility;
                config.HitEffect = hit;
                config.Sweep = sweep;
                config.BallMaterial = Material("M_CannonIron", new Color(0.18f, 0.18f, 0.2f)); // an asset, so builds include its shader
                config.YawLimit = 105f;
            });

            // Prefabs and the fixtures
            var cannon = BuildCannonPrefab(occupyInteraction, occupyAbility, fireAbility);
            EnsureCameraMount(CannonPrefab);
            var hazard = BuildHazardPrefab(health, maxHealth);
            EnsureTransformSync(HazardPrefab);
            var starboardFixture = Asset<ShipFixtureDefinition>("Assets/Settings/Fixtures/CannonStationFixture.asset", definition =>
            {
                definition.Prefab = cannon;
                definition.LocalPosition = StarboardCannonLocalPosition;
                definition.LocalEulerAngles = StarboardCannonLocalEuler;
            });
            var portFixture = Asset<ShipFixtureDefinition>("Assets/Settings/Fixtures/CannonStationPortFixture.asset", definition =>
            {
                definition.Prefab = cannon;
                definition.LocalPosition = PortCannonLocalPosition;
                definition.LocalEulerAngles = PortCannonLocalEuler;
            });
            var hazardConfig = Asset<SkyHazardConfig>("Assets/Settings/SkyHazards/SkyHazardConfig.asset", config =>
            {
                config.Prefab = hazard;
                config.FieldEnabled = true;
                // First tuning for the voyage (first-voyage.md V1): a hazard every 6 s, homing at 4 m/s, 20 unanswered hits
                // sink a 1000-health ship. Playtest 2026-09-30: they come from ahead of the bow, 90-170 m out, and each
                // player beyond the first adds two to the field and spawns them 20% sooner.
                config.MaxAlive = 4;
                config.MaxAlivePerExtraPlayer = 2;
                config.SpawnInterval = 6f;
                config.SpawnIntervalScalePerExtraPlayer = 0.8f;
                config.FieldMin = new Vector3(-45f, -10f, 90f);
                config.FieldMax = new Vector3(45f, 25f, 170f);
                config.DriftSpeed = 4f;
                config.ContactRadius = 1.5f;
                config.ImpactEffect = impact;
            });

            // Installers and profiles
            var stations = Asset<StationsFeatureInstaller>(Installers + "StationsFeatureInstaller.asset", _ => { });
            var cannons = Asset<CannonFeatureInstaller>(Installers + "CannonFeatureInstaller.asset", installer =>
            {
                SetField(installer, "_config", cannonConfig);
                SetList(installer, "_cannonFixtures", new Object[] { starboardFixture, portFixture });
            });
            var hazards = Asset<SkyHazardsFeatureInstaller>(Installers + "SkyHazardsFeatureInstaller.asset", installer =>
                SetField(installer, "_config", hazardConfig));

            var installers = new Object[] { stations, cannons, hazards };
            AddToProfile(Load<FeatureProfile>("Assets/Settings/FeatureProfiles/Profile_FuelSandbox.asset"), installers);
            var testProfile = Asset<FeatureProfile>("Assets/Settings/FeatureProfiles/Test/Profile_Test_Cannon.asset", _ => { });
            AddInclude(testProfile, Load<FeatureProfile>("Assets/Settings/FeatureProfiles/Test/Profile_Test_Core.asset"));
            AddToProfile(testProfile, installers);

            AssetDatabase.SaveAssets();
            Debug.Log("[CannonAssetBuilder] Cannon and sky-hazard assets are up to date.");
        }

        private static GameObject BuildCannonPrefab(InteractionDefinition interaction, AbilityDefinition occupy, AbilityDefinition fire)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CannonPrefab);
            if (existing != null) return existing;

            var iron = Material("M_CannonIron", new Color(0.18f, 0.18f, 0.2f));
            var wood = Material("M_CannonWood", new Color(0.45f, 0.3f, 0.18f));
            var preview = Material("M_CannonAimPreview", new Color(1f, 0.6f, 0.1f), unlit: true);

            var root = new GameObject("CannonStation");
            root.AddComponent<NetworkObject>();
            root.AddComponent<EntityNetworkMediator>();
            var mediator = root.AddComponent<CannonNetworkMediator>();
            SetField(mediator, "_interactionDefinition", interaction);
            SetField(mediator, "_occupyAbility", occupy);
            SetList(mediator, "_grantedAbilities", new Object[] { fire });

            // One collider for the whole station: what Interact aims at, and an obstacle on the deck.
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.3f, 0f);
            box.size = new Vector3(1.4f, 2.6f, 1.4f);

            Visual(PrimitiveType.Cylinder, "Carriage", root.transform, new Vector3(0f, 1.1f, 0f), Vector3.zero, new Vector3(1.2f, 1.1f, 1.2f), wood);
            var yaw = Child("YawPivot", root.transform, new Vector3(0f, 2.4f, 0f));
            Visual(PrimitiveType.Cube, "Mount", yaw, Vector3.zero, Vector3.zero, new Vector3(0.9f, 0.5f, 0.9f), wood);
            var pitch = Child("PitchPivot", yaw, Vector3.zero);
            Visual(PrimitiveType.Cylinder, "Barrel", pitch, new Vector3(0f, 0f, 1.1f), new Vector3(90f, 0f, 0f), new Vector3(0.45f, 1.1f, 0.45f), iron);
            Child("Muzzle", pitch, new Vector3(0f, 0f, 2.3f));
            Child("Seat", root.transform, new Vector3(0f, 1.05f, -1.8f));

            var line = Child("AimPreview", root.transform, Vector3.zero).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.08f;
            line.sharedMaterial = preview;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            return SavePrefab(root, CannonPrefab);
        }

        /// <summary>
        /// The gunner's view: a disabled camera (and ears) over the barrel, on the pitch pivot so it turns and tilts with
        /// the aim. Added to the existing prefab in place, so its network id is kept.
        /// </summary>
        private static void EnsureCameraMount(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pitch = root.transform.Find("YawPivot/PitchPivot");
                if (pitch == null || pitch.Find("CameraMount") != null) return;

                var mount = Child("CameraMount", pitch, new Vector3(0f, 1.1f, -3f));
                mount.localEulerAngles = new Vector3(8f, 0f, 0f);
                var camera = mount.gameObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.nearClipPlane = 0.1f;
                camera.fieldOfView = 60f;
                mount.gameObject.AddComponent<AudioListener>().enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Hazards move on the server (drift), so clients follow their position through an interpolated transform sync.</summary>
        private static void EnsureTransformSync(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<Unity.Netcode.Components.NetworkTransform>() != null) return;

                var mediator = Type.GetType(TransformMediatorType) ?? throw new InvalidOperationException($"Type {TransformMediatorType} not found.");
                var sync = (Unity.Netcode.Components.NetworkTransform)root.AddComponent(mediator);
                sync.Interpolate = true;
                sync.SyncScaleX = sync.SyncScaleY = sync.SyncScaleZ = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject BuildHazardPrefab(GameplayAttribute health, GameplayAttribute maxHealth)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HazardPrefab);
            if (existing != null) return existing;

            var root = new GameObject("SkyHazard");
            root.AddComponent<NetworkObject>();
            root.AddComponent<EntityNetworkMediator>();
            var abilityMediator = Type.GetType(AbilityMediatorType) ?? throw new InvalidOperationException($"Type {AbilityMediatorType} not found.");
            root.AddComponent(abilityMediator);
            var hazard = root.AddComponent<SkyHazardNetworkMediator>();
            SetField(hazard, "_healthAttribute", health);
            SetField(hazard, "_maxHealthAttribute", maxHealth);
            root.AddComponent<SphereCollider>().radius = 1.5f;

            Visual(PrimitiveType.Sphere, "Body", root.transform, Vector3.zero, Vector3.zero, Vector3.one * 3f, Material("M_SkyHazard", new Color(0.8f, 0.15f, 0.1f)));
            return SavePrefab(root, HazardPrefab);
        }

        private static Transform Child(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
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

        private static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureFolder(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            // The NetworkObject's id was hashed while it was a scene object, so every prefab built here would share it.
            // Its validation re-hashes it from the asset.
            var networkObject = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            return prefab;
        }

        private static Material Material(string name, Color color, bool unlit = false)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            EnsureFolder(path);
            AssetDatabase.CreateAsset(material, path);
            return material;
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
