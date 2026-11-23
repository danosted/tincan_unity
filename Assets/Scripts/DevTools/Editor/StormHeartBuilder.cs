#nullable enable
using System;
using TinCan.Features.Storm;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Storm > Build Heart: the parts of the storm's heart and its cure (plan:
    /// .docs/plans/storm-crossing.md, ST5). Puts <see cref="StormHeartNetworkMediator"/> on the encounter zone's prefab
    /// (beside its mediator; after TinCan > Dev > Encounters > Build Assets, which makes the prefab), and makes the
    /// heart's glow material (soft, additive) on the StormConfig (after TinCan > Dev > Storm > Build Lab, which makes the
    /// config). Safe to run again.
    /// </summary>
    public static class StormHeartBuilder
    {
        private const string ZonePrefabPath = "Assets/Prefabs/Encounters/EncounterZone.prefab";
        private const string ConfigPath = "Assets/Settings/Storm/StormConfig.asset";
        private const string MaterialPath = "Assets/Materials/Storm/M_StormGlow.mat";

        [MenuItem("TinCan/Dev/Storm/Build Heart")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<StormConfig>(ConfigPath)
                ?? throw new InvalidOperationException($"Missing {ConfigPath}: run TinCan > Dev > Storm > Build Lab first.");
            config.GlowMaterial = GlowMaterial();
            EditorUtility.SetDirty(config);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ZonePrefabPath) == null)
                throw new InvalidOperationException($"Missing {ZonePrefabPath}: run TinCan > Dev > Encounters > Build Assets first.");
            var root = PrefabUtility.LoadPrefabContents(ZonePrefabPath);
            try
            {
                if (root.GetComponent<StormHeartNetworkMediator>() == null)
                {
                    root.AddComponent<StormHeartNetworkMediator>();
                    PrefabUtility.SaveAsPrefabAsset(root, ZonePrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[StormHeart] The zone prefab has the storm's heart, and the config its glow.");
        }

        // URP Unlit, transparent and additive, its colour per object (StormConfig.HeartColor and
        // TargetColor through a MaterialPropertyBlock), so it reads as soft light ("Magic is soft light", ART_DIRECTION.md
        // pillar 5). Re-applied on every run.
        private static Material GlowMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_StormGlow" };
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Storm")) AssetDatabase.CreateFolder("Assets/Materials", "Storm");
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.SetFloat("_Surface", 1f); // transparent
            material.SetFloat("_Blend", 2f); // additive
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
