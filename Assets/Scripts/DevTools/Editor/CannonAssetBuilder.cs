#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using TinCan.Core.Gas;
using TinCan.Core.Gas.Cues;
using TinCan.Core.Gas.Cues.Actions;
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
    /// stable, but the cannon's look (the Blender ray cannon, .tools/blender/ray_cannon.py) is rebuilt in place every run.
    /// Plans: .docs/plans/cannon-and-hazards.md, .docs/plans/ray-cannons.md (the beam: its material and sound).
    /// </summary>
    public static class CannonAssetBuilder
    {
        private const string Tags = "Assets/Abilities/Tags/";
        private const string Effects = "Assets/Abilities/Effects/";
        private const string Abilities = "Assets/Abilities/AbilityDefinitions/";
        private const string Attributes = "Assets/Abilities/Attributes/";
        private const string Materials = "Assets/Materials/Weapons/";
        private const string CannonPrefab = "Assets/Prefabs/Weapons/CannonStation.prefab";
        private const string CannonModel = "Assets/Models/ShipParts/RayCannon/ray_cannon.fbx";
        // The children BuildLook owns: today's look, and the greybox carriage, mount and barrel it replaced.
        private static readonly string[] LookChildren = { "Carriage", "Base", "YawPivot/Mount", "YawPivot/Yoke", "YawPivot/PitchPivot/Barrel" };
        private const string HazardPrefab ="Assets/Prefabs/SkyHazards/SkyHazard.prefab";
        private const string Installers = "Assets/Resources/Installers/";
        private const string AbilityMediatorType = "TinCan.Network.Infrastructure.Abilities.AbilityNetworkMediator, TinCan.Network";
        private const string TransformMediatorType = "TinCan.Network.Infrastructure.NetworkTransformMediator, TinCan.Network";

        // A pair of broadsides on the foredeck (y = -1.815 in ship space on the airship's FloorFront and the test ship's
        // Foredeck), mirrored about the centreline x = 1. At z 17 the deck is clear between the front mast (ends z 16.2)
        // and the bow fencing, for x -2..4 (overlap-checked against the airship, 2026-09-30); the seats face inboard.
        // They look like turrets, so they turn all the way round (CannonConfig.YawLimit 180, playtest 2026-11-07); a ball
        // that hits our own ship is a misfire (plan: hazards-and-boons.md, C1).
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
            var fireTag = Tag("Ability.Cannon.Fire");
            var occupyTag = Tag("Ability.Occupy.Cannon");
            var hitCue = Tag("Cue.Hit"); // a burst on whatever was hit: GCN_Hit blinks it and bangs

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
            // The cannon fires a beam of spirit energy now (ray-cannons.md, R1): its hit and sweep keep their assets (and
            // GUIDs) under new names. The beam pulses while held and its heat limits it (R3): no reload cooldown, and each
            // pulse does a quarter of a hazard's health.
            Rename(Effects + "GE_CannonballHit.asset", "GE_SpiritBeamHit");
            Rename("Assets/Targeting/TD_CannonballSweep.asset", "TD_SpiritBeamSweep");
            var hit = Asset<GameplayEffectDefinition>(Effects + "GE_SpiritBeamHit.asset", effect =>
            {
                effect.DurationType = DurationType.Instant;
                effect.GrantedTags = new List<GameplayTag>();
                effect.Modifiers = new List<AttributeModifier>
                {
                    new() { Attribute = health, Operation = ModifierOp.Add, Value = -25f, ClampMaxAttribute = maxHealth }
                };
                effect.Cues = new List<GameplayTag> { hitCue };
            });

            var impact = Asset<GameplayEffectDefinition>(Effects + "GE_HazardImpact.asset", effect =>
            {
                effect.DurationType = DurationType.Instant;
                effect.GrantedTags = new List<GameplayTag>();
                effect.Modifiers = new List<AttributeModifier>
                {
                    new() { Attribute = health, Operation = ModifierOp.Add, Value = -50f, ClampMaxAttribute = maxHealth }
                };
                effect.Cues = new List<GameplayTag> { hitCue };
            });

            // Whatever takes a hit (ship, hazard, island-scraped hull) blinks red and bangs; shared, so on the core cues installer.
            var hitNotify = Asset<GameplayCueNotify>("Assets/Abilities/Cues/GCN_Hit.asset", notify =>
            {
                var serialized = new SerializedObject(notify);
                serialized.FindProperty("_cue").objectReferenceValue = hitCue;
                var onExecute = serialized.FindProperty("_onExecute");
                if (onExecute.arraySize > 0) // made already: keep its actions (and any tuning in the Inspector)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
                onExecute.arraySize = 2;
                onExecute.GetArrayElementAtIndex(0).managedReferenceValue = new FlashTargetCueAction();
                onExecute.GetArrayElementAtIndex(1).managedReferenceValue = new PlaySoundCueAction
                {
                    Clip = Load<AudioClip>("Assets/Audio/Cues/cue_bang.wav")
                };
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
            AddUnique(Load<GameplayCuesFeatureInstaller>(Installers + "GameplayCuesFeatureInstaller.asset"), "_cueNotifies", new Object[] { hitNotify });

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
                ability.CooldownEffect = null!; // the heat limits the beam (CannonConfig)
                ability.ActivationRequiredTagsOnActor.Add(occupyingCannon);
            });
            AssetDatabase.DeleteAsset(Effects + "GE_CannonReload.asset"); // R1's reload; nothing uses it now

            // Queries and interactions
            var sweep = Asset<TargetingDefinition>("Assets/Targeting/TD_SpiritBeamSweep.asset", definition =>
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
                config.BeamMaterial = BeamMaterial(); // an asset, so builds include its shader
                config.PulseSound = Sound("cue_spirit_beam_pulse", BeamSynth.Pulse);
                config.VentSound = Sound("cue_spirit_beam_vent", BeamSynth.Vent);
                AssetDatabase.DeleteAsset("Assets/Audio/Cues/cue_spirit_beam.wav"); // R1's charge-and-release; the pulse replaced it
                config.YawLimit = CannonAimLimits.FullTurn;
            });

            // Prefabs and the fixtures
            var cannon = BuildCannonPrefab(occupyInteraction, occupyAbility, fireAbility);
            EnsureCameraMount(CannonPrefab);
            BuildLook(CannonPrefab);
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
                // Hazards appear only on demand since H0 (hazards-and-boons.md): no field around the ship, no homing.
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

            var preview = Material("M_CannonAimPreview", new Color(1f, 0.6f, 0.1f), unlit: true);

            var root = new GameObject("CannonStation");
            root.AddComponent<NetworkObject>();
            root.AddComponent<EntityNetworkMediator>();
            var mediator = root.AddComponent<CannonNetworkMediator>();
            SetField(mediator, "_interactionDefinition", interaction);
            SetField(mediator, "_occupyAbility", occupy);
            SetList(mediator, "_grantedAbilities", new Object[] { fire });

            // The look (meshes and collider) is added by BuildLook, on every run.
            var yaw = Child("YawPivot", root.transform, new Vector3(0f, 2.4f, 0f));
            var pitch = Child("PitchPivot", yaw, Vector3.zero);
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

        /// <summary>
        /// The Blender ray cannon (.tools/blender/ray_cannon.py), in place on the existing prefab so its network id is kept:
        /// RayCannon_Base under the root as "Base", RayCannon_Yoke under YawPivot as "Yoke", RayCannon_Barrel under
        /// PitchPivot as "Barrel", each with its imported materials swapped for the shared ship materials; and one box round
        /// the post (what Interact aims at, and an obstacle on the deck). The model's pivots are the prefab's (yaw and pitch
        /// at (0, 2.4, 0), the muzzle 2.3 m along the barrel), so the parts go in where they were. Whatever was there before
        /// (the greybox carriage, mount and barrel, an older collider) is replaced.
        /// </summary>
        private static void BuildLook(string path)
        {
            var importer = AssetImporter.GetAtPath(CannonModel) as ModelImporter
                           ?? throw new InvalidOperationException($"Missing model {CannonModel}: run .tools/blender/ray_cannon.py.");
            importer.globalScale = 1f;
            importer.addCollider = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var name in LookChildren)
                {
                    var old = root.transform.Find(name);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }
                foreach (var collider in root.GetComponents<Collider>()) Object.DestroyImmediate(collider);

                var yaw = root.transform.Find("YawPivot") ?? throw new InvalidOperationException($"{path} has no YawPivot.");
                var pitch = yaw.Find("PitchPivot") ?? throw new InvalidOperationException($"{path} has no PitchPivot.");
                var shared = new Dictionary<string, Material>
                {
                    ["RayCannon_Wood"] = LookMaterials.Get("M_ShipHull"),
                    ["RayCannon_Brass"] = LookMaterials.Get("M_ShipEngine"),
                    ["RayCannon_Iron"] = LookMaterials.Get("M_ShipIron"),
                    ["RayCannon_Paint"] = LookMaterials.Get("M_ShipPennant"),
                    ["RayCannon_Spirit"] = LookMaterials.Get("M_ShipSpirit"),
                };

                // The model comes in whole at the root (both pivots sit at their place in it) and is taken apart, each
                // part keeping its world placement under the pivot that moves it.
                var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(CannonModel), root.transform);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Part(model, "RayCannon_Base", root.transform, "Base", shared);
                Part(model, "RayCannon_Yoke", yaw, "Yoke", shared);
                Part(model, "RayCannon_Barrel", pitch, "Barrel", shared);
                Object.DestroyImmediate(model);

                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 1.3f, 0f);
                box.size = new Vector3(1f, 2.6f, 1f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // The greybox's materials; nothing else uses them.
            AssetDatabase.DeleteAsset(Materials + "M_CannonIron.mat");
            AssetDatabase.DeleteAsset(Materials + "M_CannonWood.mat");
        }

        private static void Part(GameObject model, string meshName, Transform parent, string name, IReadOnlyDictionary<string, Material> shared)
        {
            var part = Array.Find(model.GetComponentsInChildren<Transform>(), t => t.name == meshName)
                       ?? throw new InvalidOperationException($"{CannonModel} has no {meshName}.");
            part.SetParent(parent, worldPositionStays: true);
            part.name = name;
            var renderer = part.GetComponent<MeshRenderer>();
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && shared.TryGetValue(materials[i].name, out var ship)) materials[i] = ship;
            }
            renderer.sharedMaterials = materials;
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

        // The beam and its glows: soft and additive, coloured by the line's (or the glow's) colour, a little over 1 so the
        // bloom picks it up softly ("Magic is soft light", ART_DIRECTION.md pillar 5). Re-applied on every run.
        private static Material BeamMaterial()
        {
            const string path = Materials + "M_SpiritBeam.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_SpiritBeam" };
                EnsureFolder(path);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_Surface", 1f); // transparent
            material.SetFloat("_Blend", 2f); // additive
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(1.6f, 1.6f, 1.6f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        // A synthesized stand-in (BeamSynth); a recording replaces it on the config without code changes.
        private static AudioClip Sound(string name, Func<float[]> synthesize)
        {
            string path = $"Assets/Audio/Cues/{name}.wav";
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, AudioLabSynth.Wav(synthesize()));
                AssetDatabase.ImportAsset(path);
            }
            return Load<AudioClip>(path);
        }

        // Renames an asset in place (its GUID, and so every reference to it, stays); nothing to do once it is renamed.
        private static void Rename(string path, string newName)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null) return;
            string error = AssetDatabase.RenameAsset(path, newName);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException($"Renaming {path} to {newName}: {error}");
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
