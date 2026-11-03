#nullable enable
using System.IO;
using System.Linq;
using NUnit.Framework;
using TinCan.DevTools.Editor;
using TinCan.Features.Atmosphere;
using UnityEditor;
using UnityEngine;

namespace TinCan.DevTools.Editor.Tests
{
    /// <summary>
    /// The look's swappable parts (plan .docs/plans/artstyle-pipeline-breakdown.md): the shading defaults are the
    /// shader's own (moving them into the look changed nothing on screen), each surface style asks for its own keyword
    /// and every style keyword is declared by the shaders that take styles, and a sweep variant overrides only what it
    /// names.
    /// </summary>
    public class LookShadingTests
    {
        private static readonly string[] StyledShaders = { "Assets/Shaders/Stylized.shader", "Assets/Shaders/SkyIsland.shader" };

        [Test]
        public void ShadingDefaults_AreTheStylizedShadersOwn()
        {
            var shader = Shader.Find(LookMaterials.ShaderName);
            float Default(string property) => shader.GetPropertyDefaultFloatValue(shader.FindPropertyIndex(property));
            var shading = ShadingSettings.Default;

            Assert.That(shading.BandSoftness, Is.EqualTo(Default("_BandSoftness")).Within(1e-5f));
            Assert.That(shading.Bands, Is.EqualTo((int)Default("_Bands")));
            Assert.That(shading.Rim, Is.EqualTo(Default("_Rim")).Within(1e-5f));
            Assert.That(shading.ShadowFill, Is.EqualTo(Default("_ShadowFill")).Within(1e-5f));
        }

        [Test]
        public void Flat_AsksForNoKeyword()
        {
            Assert.That(FlatSurfaceStyle.Instance.Evaluate().Keyword, Is.Null);
        }

        [Test]
        public void ColourDrift_AsksForItsKeyword_WithStrengthAndPatchSize()
        {
            var style = ScriptableObject.CreateInstance<ColourDriftSurfaceStyle>();
            style.Strength = 0.6f;
            style.PatchSize = 3f;
            try
            {
                var frame = style.Evaluate();
                Assert.That(frame.Keyword, Is.EqualTo(ColourDriftSurfaceStyle.Keyword));
                Assert.That(frame.Values, Is.EqualTo(new Vector4(0.6f, 3f, 0f, 0f)));

                style.Strength = 0f;
                Assert.That(style.Evaluate().Keyword, Is.Null, "no strength is flat");
            }
            finally
            {
                Object.DestroyImmediate(style);
            }
        }

        [Test]
        public void BrushEdge_AsksForItsKeyword_WithStrengthAndStrokeSize()
        {
            var style = ScriptableObject.CreateInstance<BrushEdgeSurfaceStyle>();
            style.Strength = 0.2f;
            style.StrokeSize = 0.5f;
            try
            {
                var frame = style.Evaluate();
                Assert.That(frame.Keyword, Is.EqualTo(BrushEdgeSurfaceStyle.Keyword));
                Assert.That(frame.Values, Is.EqualTo(new Vector4(0.2f, 0.5f, 0f, 0f)));

                style.Strength = 0f;
                Assert.That(style.Evaluate().Keyword, Is.Null, "no strength is flat");
            }
            finally
            {
                Object.DestroyImmediate(style);
            }
        }

        [Test]
        public void EverySurfaceStyle_UsesAListedKeyword_ThatTheStyledShadersDeclare()
        {
            var styles = TypeCache.GetTypesDerivedFrom<SurfaceStyle>().Where(t => !t.IsAbstract).ToList();
            Assert.That(styles, Is.Not.Empty);
            foreach (var type in styles)
            {
                var style = (SurfaceStyle)ScriptableObject.CreateInstance(type);
                try
                {
                    Assert.That(LookShaderGlobals.SurfaceKeywords, Does.Contain(style.Evaluate().Keyword), $"{type.Name}'s keyword is listed");
                }
                finally
                {
                    Object.DestroyImmediate(style);
                }
            }

            foreach (var path in StyledShaders)
            {
                var pragma = File.ReadAllLines(Path.Combine(LookPalette.ProjectRoot, path))
                    .FirstOrDefault(line => line.Contains("multi_compile") && line.Contains("_TINCAN_SURFACE_"));
                Assert.That(pragma, Is.Not.Null, $"{path} declares the surface styles");
                foreach (var keyword in LookShaderGlobals.SurfaceKeywords) Assert.That(pragma, Does.Contain(keyword), $"{path} declares {keyword}");
            }
        }

        [Test]
        public void AVariant_OverridesOnlyWhatItNames()
        {
            var shading = ShadingSettings.Default;
            var variant = new LookSweepVariant { OverrideBandSoftness = true, BandSoftness = 0.05f, Bands = 3, Rim = 0.9f };

            var result = variant.ShadingOver(shading);

            Assert.That(result.BandSoftness, Is.EqualTo(0.05f));
            Assert.That(result.Bands, Is.EqualTo(shading.Bands), "bands not overridden");
            Assert.That(result.Rim, Is.EqualTo(shading.Rim), "rim not overridden");
            Assert.That(result.ShadowFill, Is.EqualTo(shading.ShadowFill));
            Assert.That(variant.SurfaceOver(FlatSurfaceStyle.Instance), Is.SameAs(FlatSurfaceStyle.Instance), "surface not overridden");
            Assert.That(variant.Describe(), Is.EqualTo("soft 0.05"));
        }

        [Test]
        public void AVariant_ThatOverridesTheSurfaceWithNone_IsFlat()
        {
            var drift = ScriptableObject.CreateInstance<ColourDriftSurfaceStyle>();
            try
            {
                var variant = new LookSweepVariant { OverrideSurface = true, Surface = null };
                Assert.That(variant.SurfaceOver(drift), Is.SameAs(FlatSurfaceStyle.Instance));
                Assert.That(new LookSweepVariant().SurfaceOver(drift), Is.SameAs(drift), "no override keeps the base");
                Assert.That(new LookSweepVariant().Describe(), Is.EqualTo("base"));
            }
            finally
            {
                Object.DestroyImmediate(drift);
            }
        }

        [Test]
        public void TileLetters_RunAToZ_ThenDouble()
        {
            Assert.That(LookSweepVariant.Letter(0), Is.EqualTo("A"));
            Assert.That(LookSweepVariant.Letter(25), Is.EqualTo("Z"));
            Assert.That(LookSweepVariant.Letter(26), Is.EqualTo("AA"));
        }
    }
}
