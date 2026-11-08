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
using TinCan.Features.Boons;
using TinCan.Features.Gates;
using TinCan.Features.Gates.Actions;
using TinCan.Features.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Boons > Build Assets: the boon gates of hazards-and-boons.md (B1). Builds, or updates in place:
    /// <list type="bullet">
    /// <item>the first boon, <c>GE_Boon_SpeedBurst</c> (8 s, flight speed x1.6) with its cue <c>Cue.Boon.SpeedBurst</c>
    /// and <c>GCN_Boon_SpeedBurst</c> (a ding and a HUD line while it lasts);</item>
    /// <item>the boon gate: <c>Gate_Boon_SpeedBurst</c> (a gate definition whose action applies the boon), its networked
    /// prefab (a glowing sphere, hidden once used) and its material;</item>
    /// <item><c>BoonsConfig</c>, the Gates and Boons installers, the chart's boon material, and both installers in
    /// Profile_FuelSandbox and Profile_Test_Voyage.</item>
    /// </list>
    /// </summary>
    public static class BoonsAssetBuilder
    {
        private const string Tags = "Assets/Abilities/Tags/";
        private const string GatePrefab = "Assets/Prefabs/Boons/BoonGate.prefab";
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

            var glow = Material("Assets/Materials/Boons/M_BoonGate.mat", new Color(1f, 0.8f, 0.25f, 0.35f));
            var gate = Asset<GateDefinition>("Assets/Settings/Boons/Gate_Boon_SpeedBurst.asset", definition =>
            {
                definition.Prefab = BuildGatePrefab(glow, definition.Radius);
                if (definition.Actions.Count == 0) // made already: keep its actions (and any tuning in the Inspector)
                    definition.Actions.Add(new ApplyEffectGateAction { Effect = speedBurst });
            });
            var config = Asset<BoonsConfig>("Assets/Settings/Boons/BoonsConfig.asset", boons =>
            {
                boons.Gates = new List<GateDefinition> { gate };
            });

            var gates = Asset<GatesFeatureInstaller>(Installers + "GatesFeatureInstaller.asset", _ => { });
            var installer = Asset<BoonsFeatureInstaller>(Installers + "BoonsFeatureInstaller.asset", boons =>
            {
                SetField(boons, "_config", config);
                AddUnique(boons, "_cueNotifies", new Object[] { notify });
            });

            var navigation = Load<NavigationConfig>("Assets/Settings/Navigation/NavigationConfig.asset");
            navigation.BoonMaterial = Material("Assets/Materials/Navigation/M_Chart_Boon.mat", new Color(1f, 0.8f, 0.25f, 0.8f));
            EditorUtility.SetDirty(navigation);

            AddUnique(Load<FeatureProfile>(Profiles + "Profile_FuelSandbox.asset"), "_installers", new Object[] { gates, installer });
            AddUnique(Load<FeatureProfile>(Profiles + "Test/Profile_Test_Voyage.asset"), "_installers", new Object[] { gates, installer });

            AssetDatabase.SaveAssets();
            Debug.Log("[BoonsAssetBuilder] Boon assets are up to date.");
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
