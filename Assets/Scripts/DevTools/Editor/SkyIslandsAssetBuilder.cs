#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using TinCan.Core.Gas;
using TinCan.Features.SkyIslands;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Sky Islands > Build Assets: creates the sky islands' material (only when missing, so its colours
    /// stay as tuned), the impact effect (GE_IslandImpact: -80 health), config and installer, and adds the installer to
    /// the profiles that load it (the sandbox and the Test_Voyage area, where per-voyage layouts and impacts are tested).
    /// The config is created with its defaults and then left
    /// alone, apart from the material and the impact effect. Plan: .docs/plans/sky-islands.md.
    /// </summary>
    public static class SkyIslandsAssetBuilder
    {
        private const string MaterialPath = "Assets/Materials/SkyIslands/M_SkyIsland.mat";
        private const string ConfigPath = "Assets/Settings/SkyIslands/SkyIslandConfig.asset";
        private const string Profiles = "Assets/Settings/FeatureProfiles/";
        private const string Installers = "Assets/Resources/Installers/";
        private const string ImpactPath = "Assets/Abilities/Effects/GE_IslandImpact.asset";
        private const string Attributes = "Assets/Abilities/Attributes/";

        [MenuItem("TinCan/Dev/Sky Islands/Build Assets")]
        public static void Build()
        {
            var material = Material();
            var health = Load<GameplayAttribute>(Attributes + "Attr_Health.asset");
            var maxHealth = Load<GameplayAttribute>(Attributes + "Attr_MaxHealth.asset");
            var impact = Asset<GameplayEffectDefinition>(ImpactPath, effect =>
            {
                effect.DurationType = DurationType.Instant;
                effect.GrantedTags = new List<GameplayTag>();
                effect.Modifiers = new List<AttributeModifier>
                {
                    new() { Attribute = health, Operation = ModifierOp.Add, Value = -80f, ClampMaxAttribute = maxHealth }
                };
            });

            var config = Asset<SkyIslandConfig>(ConfigPath, islands =>
            {
                islands.Material = material;
                islands.ImpactEffect = impact;
            });
            var installer = Asset<SkyIslandsFeatureInstaller>(Installers + "SkyIslandsFeatureInstaller.asset",
                islands => SetField(islands, "_config", config));

            AddUnique(Load<FeatureProfile>(Profiles + "Profile_FuelSandbox.asset"), "_installers", new Object[] { installer });
            AddUnique(Load<FeatureProfile>(Profiles + "Test/Profile_Test_Voyage.asset"), "_installers", new Object[] { installer });

            AssetDatabase.SaveAssets();
            Debug.Log("[SkyIslandsAssetBuilder] Sky island assets built.");
        }

        private static Material Material()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            var shader = Shader.Find("TinCan/SkyIsland") ?? throw new InvalidOperationException("Shader TinCan/SkyIsland not found.");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(MaterialPath), enableInstancing = true };
            EnsureFolder(MaterialPath);
            AssetDatabase.CreateAsset(material, MaterialPath);
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
