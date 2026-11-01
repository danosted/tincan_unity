#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Look > Build Materials: the material library (.docs/ART_DIRECTION.md, "Materials"). Each entry is
    /// a material on <see cref="ShaderName"/> whose colours come from a palette role (<see cref="LookPalette"/>), with
    /// its few look settings (a metal highlight, a glow). Building rewrites each material in place (its GUID, and so
    /// every prefab's reference, stays), so a colour is tuned in palette.json, never on the material. Asset builders
    /// take their materials from <see cref="Get"/>. Plan: .docs/plans/look-slice.md (L1).
    /// </summary>
    public static class LookMaterials
    {
        public const string ShaderName = "TinCan/Stylized";
        public const string ShipFolder = "Assets/Materials/ShipParts/";
        public const string ShadowTintRole = "shadow-tint";

        public sealed class Entry
        {
            public Entry(string name, string role, float highlight = 0f, float glow = 0f)
            {
                Name = name;
                Role = role;
                Highlight = highlight;
                Glow = glow;
            }

            public string Name { get; }
            public string Path => ShipFolder + Name + ".mat";
            /// <summary>The palette role of its base colour (and of its glow).</summary>
            public string Role { get; }
            /// <summary>The metal highlight band's strength, 0 for none.</summary>
            public float Highlight { get; }
            /// <summary>How brightly it glows in its own colour (HDR, so bloom picks it up), 0 for none.</summary>
            public float Glow { get; }
        }

        public static IReadOnlyList<Entry> Library { get; } = new[]
        {
            new Entry("M_ShipWalls", "timber-dark"),
            new Entry("M_ShipHull", "timber-warm"),
            new Entry("M_ShipDeck", "deck"),
            new Entry("M_ShipEngine", "brass", highlight: 0.6f),
            new Entry("M_ShipIron", "iron-teal", highlight: 0.35f),
            new Entry("M_ShipCanvas", "canvas"),
            new Entry("M_ShipPennant", "lacquer-red"),
            new Entry("M_ShipCairn", "rock-ochre"),
            new Entry("M_ShipSpirit", "spirit-cyan", glow: 1.6f),
        };

        [MenuItem("TinCan/Dev/Look/Build Materials")]
        public static void Build()
        {
            var palette = LookPalette.Load();
            foreach (var entry in Library) Write(entry, palette);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Look] {Library.Count} library materials built on {ShaderName}");
        }

        public static Entry Find(string name) =>
            Library.FirstOrDefault(e => e.Name == name) ?? throw new ArgumentException($"no library material named '{name}'");

        /// <summary>A library material, created or brought in step with the palette.</summary>
        public static Material Get(string name) => Write(Find(name), LookPalette.Load());

        private static Material Write(Entry entry, LookPalette palette)
        {
            var shader = Shader.Find(ShaderName) ?? throw new InvalidOperationException($"shader {ShaderName} not found");
            var built = new Material(shader) { enableInstancing = true };
            built.SetColor("_BaseColor", palette[entry.Role]);
            built.SetColor("_ShadowTint", palette[ShadowTintRole]);
            built.SetFloat("_Highlight", entry.Highlight);
            var glow = entry.Glow > 0f ? palette[entry.Role] * entry.Glow : Color.black;
            glow.a = 1f;
            built.SetColor("_EmissionColor", glow);

            var material = AssetDatabase.LoadAssetAtPath<Material>(entry.Path);
            if (material == null)
            {
                built.name = entry.Name;
                AssetDatabase.CreateAsset(built, entry.Path);
                return built;
            }

            // Copied over the old one, so properties of a former shader do not linger in the asset.
            EditorUtility.CopySerialized(built, material);
            material.name = entry.Name;
            EditorUtility.SetDirty(material);
            UnityEngine.Object.DestroyImmediate(built);
            return material;
        }
    }
}
