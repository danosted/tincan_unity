#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship.Parts;
using TinCan.DevTools.Editor;
using TinCan.Features.ShipDesigns;
using UnityEditor;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The material library (plan .docs/plans/look-slice.md, L1): every library material is on TinCan/Stylized with its
    /// palette colours, and the ship parts and the frame hull use library materials only. The helm is left out until
    /// its third-party meshes are replaced (.docs/plans/remove-old-third-party-content.md, step 2).
    /// </summary>
    public class LookMaterialsTests
    {
        private static readonly string[] NotYetOnTheLibrary = { ShipDesignsAssetBuilder.CoreHelm };

        private static Material Load(LookMaterials.Entry entry) =>
            AssetDatabase.LoadAssetAtPath<Material>(entry.Path) ?? throw new AssertionException($"missing {entry.Path}: run TinCan > Dev > Look > Build Materials");

        [Test]
        public void EveryLibraryMaterial_IsStylized_WithItsPaletteColours()
        {
            var palette = LookPalette.Load();
            foreach (var entry in LookMaterials.Library)
            {
                var material = Load(entry);
                Assert.That(material.shader.name, Is.EqualTo(LookMaterials.ShaderName), entry.Name);
                Assert.That(Distance(material.GetColor("_BaseColor"), palette[entry.Role]), Is.LessThan(0.002f), $"{entry.Name} base colour is {entry.Role}");
                Assert.That(Distance(material.GetColor("_ShadowTint"), palette[LookMaterials.ShadowTintRole]), Is.LessThan(0.002f), $"{entry.Name} shadow tint");
                Assert.That(material.GetColor("_EmissionColor").maxColorComponent > 0f, Is.EqualTo(entry.Glow > 0f), $"{entry.Name} glows only when it should");
                Assert.That(material.GetFloat("_OwnBandSoftness") > 0.5f, Is.EqualTo(entry.OwnBandSoftness.HasValue), $"{entry.Name} keeps its own softness only when the table says");
                Assert.That(material.GetFloat("_OwnBands") > 0.5f, Is.EqualTo(entry.OwnBands.HasValue), $"{entry.Name} keeps its own bands only when the table says");
                Assert.That(material.GetFloat("_OwnRim") > 0.5f, Is.EqualTo(entry.OwnRim.HasValue), $"{entry.Name} keeps its own rim only when the table says");
                Assert.That(material.GetFloat("_OwnShadowFill") > 0.5f, Is.EqualTo(entry.OwnShadowFill.HasValue), $"{entry.Name} keeps its own shadow fill only when the table says");
            }
        }

        [Test]
        public void ShipParts_AndTheFrameHull_UseLibraryMaterialsOnly()
        {
            var library = LookMaterials.Library.Select(Load).ToHashSet();
            var config = AssetDatabase.LoadAssetAtPath<ShipDesignsConfig>(ShipDesignsAssetBuilder.ConfigPath);
            Assert.That(new[] { config.FramePlanks, config.FrameBeams, config.FrameWalls }, Is.SubsetOf(library), "frame materials");

            var parts = AssetDatabase.FindAssets("t:FeatureInstaller", new[] { "Assets/Resources/Installers" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FeatureInstaller>(AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<FeatureInstaller.IExtension<ShipPartDefinition>>()
                .SelectMany(e => e.Contributions)
                .Where(p => !NotYetOnTheLibrary.Contains(p.PartId))
                .ToList();
            Assert.That(parts, Is.Not.Empty);
            foreach (var part in parts)
            {
                var prefab = part.Visual != null ? part.Visual : part.NetworkedPrefab;
                Assert.That(prefab, Is.Not.Null, part.PartId);
                foreach (var renderer in prefab!.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.sharedMaterials, Is.SubsetOf(library), $"{part.PartId}: {renderer.name}");
                }
            }
        }

        private static float Distance(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
    }
}
