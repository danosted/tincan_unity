#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Gas;
using TinCan.Features.Airship.Damage;
using TinCan.Features.SkillChecks;
using UnityEditor;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Builds repair as a check (plan hazards-and-boons.md, R1): the repair check SC_ShipRepair (a pulsing gauge, no
    /// rest, so a Miss can be retried at once; Perfect fixes the part, Good heals half of it), and a check host beside
    /// every breakable point (the fixture's points and the designed ship's point prefab), and the SkillChecks installer in
    /// the ShipDamage test profile. Safe to run again.
    /// </summary>
    public static class RepairCheckAssetBuilder
    {
        private const string OldTick = "Assets/Abilities/Effects/GE_RepairTick.asset";
        private const string Good = "Assets/Abilities/Effects/GE_ShipPartRepairGood.asset";
        private const string Check = "Assets/Settings/ShipDamage/SC_ShipRepair.asset";
        private static readonly string[] PointPrefabs =
        {
            "Assets/Prefabs/Airship/Parts/ShipDamagePoint.prefab",
            "Assets/Prefabs/Airship/Parts/ShipDamageSockets.prefab",
        };

        // Repairs need the checks wherever ShipDamage loads; the other profiles with it have them already.
        private const string ShipDamageProfile = "Assets/Settings/FeatureProfiles/Test/Profile_Test_ShipDamage.asset";
        private const string SkillChecksInstaller = "Assets/Resources/Installers/SkillChecksFeatureInstaller.asset";

        [MenuItem("TinCan/Dev/Ship Damage/Build Repair Check")]
        public static void Build()
        {
            // The old per-tick heal becomes the Good grade's heal (its GUID kept).
            if (AssetDatabase.LoadAssetAtPath<GameplayEffectDefinition>(Good) == null && AssetDatabase.LoadAssetAtPath<GameplayEffectDefinition>(OldTick) != null)
            {
                string error = AssetDatabase.RenameAsset(OldTick, Path.GetFileNameWithoutExtension(Good));
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }

            var health = Load<HealthAttribute>("Assets/Abilities/Attributes/Attr_Health.asset");
            var maxHealth = Load<MaxHealthAttribute>("Assets/Abilities/Attributes/Attr_MaxHealth.asset");
            var good = Asset<GameplayEffectDefinition>(Good, effect =>
            {
                effect.DurationType = DurationType.Instant;
                effect.GrantedTags = new List<TinCan.Core.Domain.Abilities.Tags.GameplayTag>();
                effect.Modifiers = new List<AttributeModifier>
                {
                    new() { Attribute = health, Operation = ModifierOp.Add, Value = 50f, ClampMaxAttribute = maxHealth }
                };
            });
            var restore = Load<GameplayEffectDefinition>("Assets/Abilities/Effects/GE_ShipPartRestore.asset");

            var check = Asset<SkillCheckDefinition>(Check, definition =>
            {
                definition.Title = "Patch it";
                definition.Station = "Repair";
                definition.Kind = SkillCheckKind.PulsingGauge;
                definition.Cooldown = 0f;
                definition.GoodEffect = good;
                definition.PerfectEffect = restore;
                definition.BonusTag = null;
                definition.BonusEffect = null;
            });

            foreach (var path in PointPrefabs) AddHosts(path, check);
            AddInstaller(ShipDamageProfile, Load<UnityEngine.Object>(SkillChecksInstaller));
            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(RepairCheckAssetBuilder)}] Built {Check} and the hosts on {PointPrefabs.Length} prefabs.");
        }

        // A host on every breakable point, added after its other components (the point prefab's actor stays the first one).
        private static void AddHosts(string path, SkillCheckDefinition check)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var point in root.GetComponentsInChildren<ShipDamagePointNetworkMediator>(true))
                {
                    var host = point.GetComponent<SkillCheckHostNetworkMediator>() ?? point.gameObject.AddComponent<SkillCheckHostNetworkMediator>();
                    var serialized = new SerializedObject(host);
                    serialized.FindProperty("_check").objectReferenceValue = check;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // So the NetworkObject's validated hash is written (CODE_MAP, known traps: a stale GlobalObjectIdHash).
            EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }

        private static void AddInstaller(string profilePath, UnityEngine.Object installer)
        {
            var profile = Load<ScriptableObject>(profilePath);
            var serialized = new SerializedObject(profile);
            var list = serialized.FindProperty("_installers");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == installer) return;
            }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = installer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static T Asset<T>(string path, Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                var folder = Path.GetDirectoryName(path)!.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)!.Replace('\\', '/'), Path.GetFileName(folder));
                AssetDatabase.CreateAsset(asset, path);
            }

            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Missing asset {path}.");
    }
}
