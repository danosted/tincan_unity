#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Gas;
using TinCan.Core.Gas.Cues;
using TinCan.Core.Gas.Cues.Actions;
using TinCan.Core.Interaction;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.Boons;
using TinCan.Features.Gates;
using TinCan.Features.Gates.Actions;
using TinCan.Features.Navigation;
using TinCan.Features.SkillChecks;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Boons > Build Assets: the boon gates and the engine boost of hazards-and-boons.md (B1, B3). Builds,
    /// or updates in place:
    /// <list type="bullet">
    /// <item>the first boon, <c>GE_Boon_SpeedBurst</c> (8 s, flight speed x1.6) with its cue <c>Cue.Boon.SpeedBurst</c>
    /// and <c>GCN_Boon_SpeedBurst</c> (a ding and a HUD line while it lasts);</item>
    /// <item>the boon gate: <c>Gate_Boon_SpeedBurst</c> (a gate definition whose action applies the boon), its networked
    /// prefab (a glowing sphere, hidden once used) and its material;</item>
    /// <item>the boost window (B3): <c>GE_Boon_BoostWindow</c> (15 s, <c>State.Ship.BoostWindow</c>, a HUD line), put on
    /// the ship by the gate as a second action;</item>
    /// <item>the engine telegraph (B3): its skill check <c>SC_EngineBoost</c> (Good <c>GE_Boon_EngineGood</c>, Perfect
    /// <c>GE_Boon_EnginePerfect</c>, inside a window also <c>GE_Boon_EngineExtra</c>, each with its cue and notify),
    /// <c>IA_EngineBoost</c>, the prefab, its fixture on the old hull and the fitting <c>boon.engine_telegraph</c>;</item>
    /// <item><c>BoonsConfig</c>, the Gates, SkillChecks and Boons installers, the chart's boon material, and the
    /// installers in Profile_FuelSandbox and Profile_Test_Voyage. Run TinCan > Dev > Input > Build Assets after, for the
    /// skill check's input context.</item>
    /// </list>
    /// </summary>
    public static class BoonsAssetBuilder
    {
        private const string Tags = "Assets/Abilities/Tags/";
        private const string GatePrefab = "Assets/Prefabs/Boons/BoonGate.prefab";
        private const string TelegraphPrefab = "Assets/Prefabs/Boons/EngineTelegraph.prefab";
        private const string Effects = "Assets/Abilities/Effects/";
        private const string Cues = "Assets/Abilities/Cues/";
        private const string Installers = "Assets/Resources/Installers/";
        private const string Profiles = "Assets/Settings/FeatureProfiles/";

        [MenuItem("TinCan/Dev/Boons/Build Assets")]
        public static void Build()
        {
            var flightSpeed = Load<GameplayAttribute>("Assets/Abilities/Attributes/Attr_FlightSpeed.asset");
            var cue = Asset<GameplayTag>(Tags + "Cue.Boon.SpeedBurst.asset", _ => { });
            var speedBurst = Asset<GameplayEffectDefinition>("Assets/Abilities/Effects/GE_Boon_SpeedBurst.asset", effect =>
            {
                effect.DurationType = DurationType.Duration;
                effect.DurationSeconds = 8f;
                effect.GrantedTags = new List<GameplayTag>();
                effect.Modifiers = new List<AttributeModifier>
                {
                    new() { Attribute = flightSpeed, Operation = ModifierOp.Multiply, Value = 1.6f }
                };
                effect.Cues = new List<GameplayTag> { cue };
            });

            var notify = Asset<GameplayCueNotify>("Assets/Abilities/Cues/GCN_Boon_SpeedBurst.asset", asset =>
            {
                var serialized = new SerializedObject(asset);
                serialized.FindProperty("_cue").objectReferenceValue = cue;
                var onActive = serialized.FindProperty("_onActive");
                if (onActive.arraySize == 0) // made already: keep its actions (and any tuning in the Inspector)
                {
                    onActive.arraySize = 2;
                    onActive.GetArrayElementAtIndex(0).managedReferenceValue = new PlaySoundCueAction
                    {
                        Clip = Load<AudioClip>("Assets/Audio/Cues/cue_ding.wav")
                    };
                    onActive.GetArrayElementAtIndex(1).managedReferenceValue = new HudToastCueAction { Text = "Speed burst!", Seconds = 3f };
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

            // The boost window a gate opens, and the engine telegraph's check (B3).
            var windowTag = Asset<GameplayTag>(Tags + "State.Ship.BoostWindow.asset", _ => { });
            var window = Effect("GE_Boon_BoostWindow", 15f, null, 1f, Cue("BoostWindow"), windowTag);
            var engineGood = Effect("GE_Boon_EngineGood", 6f, flightSpeed, 1.3f, Cue("EngineGood"));
            var enginePerfect = Effect("GE_Boon_EnginePerfect", 8f, flightSpeed, 1.5f, Cue("EnginePerfect"));
            var engineExtra = Effect("GE_Boon_EngineExtra", 8f, flightSpeed, 1.3f, Cue("EngineExtra"));
            var notifies = new Object[]
            {
                notify,
                Notify("BoostWindow", Cue("BoostWindow"), null, "Boost window: run the engine telegraph!", 5f),
                Notify("EngineGood", Cue("EngineGood"), "cue_ding", "Engine boost!", 3f),
                Notify("EnginePerfect", Cue("EnginePerfect"), "cue_ding", "Perfect engine boost!", 3f),
                Notify("EngineExtra", Cue("EngineExtra"), "cue_ding", "Extra boost from the gate!", 3f),
            };
            var check = Asset<SkillCheckDefinition>("Assets/Settings/Boons/SC_EngineBoost.asset", definition =>
            {
                definition.Title = "Engine boost";
                definition.GoodEffect = engineGood;
                definition.PerfectEffect = enginePerfect;
                definition.BonusTag = windowTag;
                definition.BonusEffect = engineExtra;
            });
            var interaction = Asset<InteractionDefinition>("Assets/Interactions/IA_EngineBoost.asset", asset =>
            {
                var serialized = new SerializedObject(asset);
                serialized.FindProperty("_handlerTypeName").stringValue = typeof(StartEngineBoostInteractionHandler).AssemblyQualifiedName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
            var telegraph = BuildTelegraphPrefab(check, interaction,
                LitMaterial("Assets/Materials/Boons/M_EngineTelegraph.mat", new Color(0.72f, 0.55f, 0.25f)));
            var fixture = Asset<ShipFixtureDefinition>("Assets/Settings/Fixtures/EngineTelegraphFixture.asset", definition =>
            {
                definition.Prefab = telegraph;
                definition.LocalPosition = new Vector3(-3f, -3.49f, 5f);
                definition.LocalEulerAngles = new Vector3(0f, 90f, 0f);
            });
            var fitting = Asset<ShipFittingDefinition>("Assets/Settings/ShipFittings/FITTING_EngineTelegraph.asset", asset =>
            {
                var serialized = new SerializedObject(asset);
                serialized.FindProperty("_fittingId").stringValue = "boon.engine_telegraph";
                serialized.FindProperty("_displayName").stringValue = "Engine telegraph";
                serialized.FindProperty("_prefab").objectReferenceValue = telegraph;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
            var skillChecks = Asset<SkillChecksFeatureInstaller>(Installers + "SkillChecksFeatureInstaller.asset", asset =>
                AddUnique(asset, "_styles", new Object[] { Load<StyleSheet>("Assets/Scripts/Features/SkillChecks/UI/SkillGauge.uss") }));

            var glow = Material("Assets/Materials/Boons/M_BoonGate.mat", new Color(1f, 0.8f, 0.25f, 0.35f));
            var gate = Asset<GateDefinition>("Assets/Settings/Boons/Gate_Boon_SpeedBurst.asset", definition =>
            {
                definition.Prefab = BuildGatePrefab(glow, definition.Radius);
                if (definition.Actions.Count == 0) // made already: keep its actions (and any tuning in the Inspector)
                    definition.Actions.Add(new ApplyEffectGateAction { Effect = speedBurst });
                if (!definition.Actions.Exists(action => action is ApplyEffectGateAction { Effect: var effect } && effect == window))
                    definition.Actions.Add(new ApplyEffectGateAction { Effect = window });
            });
            var config = Asset<BoonsConfig>("Assets/Settings/Boons/BoonsConfig.asset", boons =>
            {
                boons.Gates = new List<GateDefinition> { gate };
            });

            var gates = Asset<GatesFeatureInstaller>(Installers + "GatesFeatureInstaller.asset", _ => { });
            var installer = Asset<BoonsFeatureInstaller>(Installers + "BoonsFeatureInstaller.asset", boons =>
            {
                SetField(boons, "_config", config);
                AddUnique(boons, "_cueNotifies", notifies);
                SetField(boons, "_telegraphFixture", fixture);
                AddUnique(boons, "_fittings", new Object[] { fitting });
            });

            var navigation = Load<NavigationConfig>("Assets/Settings/Navigation/NavigationConfig.asset");
            navigation.BoonMaterial = Material("Assets/Materials/Navigation/M_Chart_Boon.mat", new Color(1f, 0.8f, 0.25f, 0.8f));
            EditorUtility.SetDirty(navigation);

            AddUnique(Load<FeatureProfile>(Profiles + "Profile_FuelSandbox.asset"), "_installers", new Object[] { gates, skillChecks, installer });
            AddUnique(Load<FeatureProfile>(Profiles + "Test/Profile_Test_Voyage.asset"), "_installers", new Object[] { gates, skillChecks, installer });

            AssetDatabase.SaveAssets();
            Debug.Log("[BoonsAssetBuilder] Boon assets are up to date.");
        }

        private static GameplayTag Cue(string name) => Asset<GameplayTag>(Tags + "Cue.Boon." + name + ".asset", _ => { });

        /// <summary>A timed effect: the attribute times <paramref name="multiplier"/> (no modifier without one), its cue, the tags it grants.</summary>
        private static GameplayEffectDefinition Effect(string name, float seconds, GameplayAttribute? attribute, float multiplier,
            GameplayTag cue, params GameplayTag[] granted) =>
            Asset<GameplayEffectDefinition>(Effects + name + ".asset", effect =>
            {
                effect.DurationType = DurationType.Duration;
                effect.DurationSeconds = seconds;
                effect.GrantedTags = new List<GameplayTag>(granted);
                effect.Modifiers = attribute == null
                    ? new List<AttributeModifier>()
                    : new List<AttributeModifier> { new() { Attribute = attribute, Operation = ModifierOp.Multiply, Value = multiplier } };
                effect.Cues = new List<GameplayTag> { cue };
            });

        /// <summary>GCN_Boon_*: while the cue is active, a sound (if named) and a HUD line. Made once; later runs keep its actions.</summary>
        private static GameplayCueNotify Notify(string name, GameplayTag cue, string? sound, string text, float seconds) =>
            Asset<GameplayCueNotify>(Cues + "GCN_Boon_" + name + ".asset", asset =>
            {
                var serialized = new SerializedObject(asset);
                serialized.FindProperty("_cue").objectReferenceValue = cue;
                var onActive = serialized.FindProperty("_onActive");
                if (onActive.arraySize == 0)
                {
                    var actions = new List<GameplayCueAction>();
                    if (sound != null) actions.Add(new PlaySoundCueAction { Clip = Load<AudioClip>("Assets/Audio/Cues/" + sound + ".wav") });
                    actions.Add(new HudToastCueAction { Text = text, Seconds = seconds });
                    onActive.arraySize = actions.Count;
                    for (int i = 0; i < actions.Count; i++) onActive.GetArrayElementAtIndex(i).managedReferenceValue = actions[i];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

        /// <summary>
        /// The engine telegraph: a NetworkObject with its entity, skill check host and telegraph mediators; a brass post
        /// with a dial on top (visuals without colliders) and a trigger at chest height for the interaction ray. Its pivot
        /// is at its base, so it stands on a deck or a socket.
        /// </summary>
        private static GameObject BuildTelegraphPrefab(SkillCheckDefinition check, InteractionDefinition interaction, Material brass)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPrefab);
            if (existing != null) return existing;

            var root = new GameObject("EngineTelegraph");
            root.AddComponent<NetworkObject>();
            root.AddComponent<EntityNetworkMediator>();
            SetField(root.AddComponent<SkillCheckHostNetworkMediator>(), "_check", check);
            SetField(root.AddComponent<EngineTelegraphNetworkMediator>(), "_interactionDefinition", interaction);

            Part(root, PrimitiveType.Cylinder, "Post", new Vector3(0f, 0.55f, 0f), new Vector3(0.14f, 0.55f, 0.14f), Vector3.zero, brass);
            Part(root, PrimitiveType.Cylinder, "Dial", new Vector3(0f, 1.25f, 0f), new Vector3(0.5f, 0.06f, 0.5f), new Vector3(90f, 0f, 0f), brass);

            var volume = new GameObject("InteractVolume");
            volume.transform.SetParent(root.transform, false);
            volume.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var box = volume.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.6f, 1.6f, 0.6f);

            EnsureFolder(TelegraphPrefab);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TelegraphPrefab);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Part(GameObject root, PrimitiveType shape, string name, Vector3 position, Vector3 scale, Vector3 euler, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localEulerAngles = euler;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material LitMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP shader not found.");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0.6f);
            material.SetFloat("_Smoothness", 0.5f);
            EnsureFolder(path);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>The boon gate's prefab: a NetworkObject with its entity and gate mediators, and a glowing sphere the size of its reach, without a collider.</summary>
        private static GameObject BuildGatePrefab(Material glow, float radius)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefab);
            if (existing != null) return existing;

            var root = new GameObject("BoonGate");
            root.AddComponent<NetworkObject>();
            root.AddComponent<EntityNetworkMediator>();
            root.AddComponent<GateNetworkMediator>();

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = GateNetworkMediator.GlowName;
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localScale = Vector3.one * (radius * 2f);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = glow;

            EnsureFolder(GatePrefab);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, GatePrefab);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // An unlit, see-through colour (URP's transparent surface, set the way its material inspector sets it).
        private static Material Material(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new InvalidOperationException("URP shader not found.");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
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

        private static void SetField(Object target, string field, Object? value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddUnique(Object target, string field, IEnumerable<Object> values)
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
