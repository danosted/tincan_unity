#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinCan.Features.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Renders look sweeps (<see cref="LookSweep"/>; plan .docs/plans/artstyle-pipeline-breakdown.md, P1): each variant
    /// of the base look on each subject, one lettered tile per variant, into
    /// <see cref="LookContactSheet.OutputFolder"/>/sweep-&lt;name&gt;.png, with a JSON legend of every tile's values beside
    /// it. Lit and aired like the contact sheet (a preview scene, the base look's sun, air and post); a variant changes
    /// only the shader globals the game itself sets (<see cref="LookShaderGlobals"/>), so a tile is what play shows.
    /// <see cref="Apply"/> writes a chosen variant into the base look. Menus: TinCan > Dev > Look > Sweeps.
    /// </summary>
    public static class LookSweepSheet
    {
        public const string SweepFolder = "Assets/Settings/Look/Sweeps/";
        public const string SurfaceFolder = "Assets/Settings/Look/Surface/";
        private const string Menu = "TinCan/Dev/Look/Sweeps/";
        private const int TileWidth = 640;
        // 16:9, as LookContactSheet frames its views.
        private const int TileHeight = 360;
        private const int MostColumns = 5;
        private const int WrappedColumns = 4;
        /// <summary>Subjects are built this far apart, so no view sees another subject.</summary>
        private const float SubjectSpacing = 6000f;
        private const float ShapesHeight = 60f;

        [MenuItem(Menu + "Render All")]
        public static void RenderAllMenu()
        {
            var paths = Sweeps().Select(Render).ToList();
            Debug.Log(paths.Count == 0
                ? $"[Look] no sweeps in {SweepFolder}: run {Menu}Build Defaults"
                : $"[Look] sweeps: {string.Join(", ", paths)}");
            if (paths.Count > 0) EditorUtility.RevealInFinder(paths[0]);
        }

        [MenuItem("Assets/TinCan/Render Look Sweep")]
        public static void RenderSelectedMenu()
        {
            foreach (var sweep in Selection.objects.OfType<LookSweep>()) Debug.Log($"[Look] sweep: {Render(sweep)}");
        }

        [MenuItem("Assets/TinCan/Render Look Sweep", true)]
        public static bool RenderSelectedValidate() => Selection.objects.OfType<LookSweep>().Any();

        public static IEnumerable<LookSweep> Sweeps() =>
            AssetDatabase.FindAssets("t:" + nameof(LookSweep), new[] { SweepFolder.TrimEnd('/') })
                .Select(guid => AssetDatabase.LoadAssetAtPath<LookSweep>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(s => s != null);

        /// <summary>The look a sweep changes: its base, or the golden-afternoon look.</summary>
        public static LookProfile BaseOf(LookSweep sweep) =>
            sweep.Base != null
                ? sweep.Base
                : AssetDatabase.LoadAssetAtPath<LookProfile>(AtmosphereAssetBuilder.LookPath)
                  ?? throw new InvalidOperationException($"no look at {AtmosphereAssetBuilder.LookPath}: run TinCan > Dev > Look > Build Atmosphere");

        /// <summary>Renders a sweep and returns the sheet's full path.</summary>
        public static string Render(LookSweep sweep)
        {
            if (sweep.Variants.Count == 0) throw new InvalidOperationException($"sweep {sweep.name} has no variants");
            var look = BaseOf(sweep);
            var palette = LookPalette.Load();
            var subjects = sweep.Subjects.Length > 0 ? sweep.Subjects.Distinct().ToArray() : new[] { LookSweepSubject.Ship };
            int count = sweep.Variants.Count;
            int columns = count <= MostColumns ? count : WrappedColumns;
            int rowsPerSubject = (count + columns - 1) / columns;

            // Compile shader variants on the spot: asynchronous compilation would leave the first tiles blank.
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var scene = EditorSceneManager.NewPreviewScene();
            var grid = new LookSheetGrid(columns, rowsPerSubject * subjects.Length, TileWidth, TileHeight);
            GameObject? volume = null;
            try
            {
                LookContactSheet.AddSun(scene, palette, look);
                var camera = LookContactSheet.AddCamera(scene, palette, look, grid.Target);
                volume = LookContactSheet.ApplyLook(look);

                for (int s = 0; s < subjects.Length; s++)
                {
                    Stage(subjects[s], scene, camera, look, new Vector3(s * SubjectSpacing, 0f, 0f));
                    // A render per variant first: the first render with a new style keyword can come out blank, even
                    // with the variant compiled on the spot.
                    camera.targetTexture = grid.Target;
                    foreach (var variant in sweep.Variants)
                    {
                        LookShaderGlobals.Apply(variant.ShadingOver(look.Shading), variant.SurfaceOver(look.ActiveSurface).Evaluate());
                        camera.Render();
                    }

                    for (int i = 0; i < count; i++)
                    {
                        var variant = sweep.Variants[i];
                        LookShaderGlobals.Apply(variant.ShadingOver(look.Shading), variant.SurfaceOver(look.ActiveSurface).Evaluate());
                        grid.Capture(camera, i % columns, s * rowsPerSubject + i / columns, $"{LookSweepVariant.Letter(i)} {variant.Describe()}");
                    }
                }

                var path = grid.Write($"sweep-{sweep.name}.png");
                File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonUtility.ToJson(Legend(sweep, look), prettyPrint: true));
                return path;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                LookContactSheet.ClearLook(volume);
                // The camera goes with the scene first: its target is the grid's.
                EditorSceneManager.ClosePreviewScene(scene);
                grid.Dispose();
            }
        }

        /// <summary>Writes a variant into the sweep's base look (undoable): the choice is made.</summary>
        public static void Apply(LookSweep sweep, int index)
        {
            var look = BaseOf(sweep);
            var variant = sweep.Variants[index];
            Undo.RecordObject(look, $"Apply look sweep {sweep.name} {LookSweepVariant.Letter(index)}");
            look.Shading = variant.ShadingOver(look.Shading);
            if (variant.OverrideSurface) look.Surface = variant.Surface;
            EditorUtility.SetDirty(look);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Look] {look.name} takes {sweep.name} {LookSweepVariant.Letter(index)} ({variant.Describe()})");
        }

        private static void Stage(LookSweepSubject subject, Scene scene, Camera camera, LookProfile look, Vector3 offset)
        {
            switch (subject)
            {
                case LookSweepSubject.Ship:
                {
                    var ship = LookContactSheet.BuildShip(scene, LookContactSheet.DefaultDesign, out _);
                    ship.transform.position += offset;
                    camera.fieldOfView = 30f;
                    LookContactSheet.AimThreeQuarter(camera, ship);
                    break;
                }
                case LookSweepSubject.Shapes:
                {
                    // Lit from the side (at right angles to the sunlight), so lit band, step and shadow band all show.
                    var facing = Quaternion.Euler(0f, look.Settings.SunAngles.y - 90f, 0f);
                    var across = facing * Vector3.right;
                    var centre = offset + Vector3.up * ShapesHeight;
                    LookLabSheet.Shape(scene, PrimitiveType.Cube, centre + Vector3.down * 1.2f, new Vector3(13f, 0.4f, 5f),
                        LookLabSheet.Library("M_ShipDeck"), facing);
                    var items = new (PrimitiveType Type, string Material, Vector3 Scale, float Lift, Quaternion Turn)[]
                    {
                        (PrimitiveType.Sphere, "M_ShipCanvas", Vector3.one * 2.4f, 0.2f, Quaternion.identity),
                        (PrimitiveType.Cube, "M_ShipHull", Vector3.one * 1.8f, -0.1f, facing * Quaternion.Euler(0f, 45f, 0f)),
                        (PrimitiveType.Cylinder, "M_ShipIron", new Vector3(1.6f, 1.1f, 1.6f), 0.1f, facing),
                        (PrimitiveType.Sphere, "M_ShipPennant", Vector3.one * 2.4f, 0.2f, Quaternion.identity),
                    };
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i];
                        LookLabSheet.Shape(scene, item.Type, centre + across * ((i - 1.5f) * 3f) + Vector3.up * item.Lift, item.Scale,
                            LookLabSheet.Library(item.Material), item.Turn);
                    }

                    camera.fieldOfView = 30f;
                    var view = facing * Quaternion.Euler(12f, 0f, 0f);
                    camera.transform.SetPositionAndRotation(centre - view * Vector3.forward * 17f, view);
                    break;
                }
            }
        }

        [Serializable]
        private sealed class SweepLegend
        {
            public string Sweep = "";
            public string Base = "";
            public List<SweepLegendTile> Tiles = new();
        }

        [Serializable]
        private sealed class SweepLegendTile
        {
            public string Letter = "";
            public string Label = "";
            public float BandSoftness;
            public int Bands;
            public float Rim;
            public float ShadowFill;
            public string Surface = "";
        }

        private static SweepLegend Legend(LookSweep sweep, LookProfile look)
        {
            var legend = new SweepLegend { Sweep = sweep.name, Base = look.name };
            for (int i = 0; i < sweep.Variants.Count; i++)
            {
                var variant = sweep.Variants[i];
                var shading = variant.ShadingOver(look.Shading);
                var surface = variant.SurfaceOver(look.ActiveSurface);
                legend.Tiles.Add(new SweepLegendTile
                {
                    Letter = LookSweepVariant.Letter(i),
                    Label = variant.Describe(),
                    BandSoftness = shading.BandSoftness,
                    Bands = shading.Bands,
                    Rim = shading.Rim,
                    ShadowFill = shading.ShadowFill,
                    Surface = surface is UnityEngine.Object named ? named.name : "flat",
                });
            }

            return legend;
        }

        /// <summary>The first sweeps and the surface style assets they compare; existing ones are left as tuned.</summary>
        [MenuItem(Menu + "Build Defaults")]
        public static void BuildDefaults()
        {
            var driftFaint = Style<ColourDriftSurfaceStyle>("Surface_ColourDrift_Faint", s => { s.Strength = 0.35f; s.PatchSize = 2.5f; });
            var driftStrong = Style<ColourDriftSurfaceStyle>("Surface_ColourDrift_Strong", s => { s.Strength = 0.8f; s.PatchSize = 2.5f; });
            var brushFaint = Style<BrushEdgeSurfaceStyle>("Surface_BrushEdge_Faint", s => { s.Strength = 0.12f; s.StrokeSize = 0.35f; });
            var brushStrong = Style<BrushEdgeSurfaceStyle>("Surface_BrushEdge_Strong", s => { s.Strength = 0.3f; s.StrokeSize = 0.35f; });

            // Columns band softness (0.35 is today's), rows two then three bands.
            Sweep("Sweep_Bands", new[] { LookSweepSubject.Ship, LookSweepSubject.Shapes },
                from bands in new[] { 2, 3 }
                from softness in new[] { 0.05f, 0.1f, 0.2f, 0.35f }
                select new LookSweepVariant { OverrideBandSoftness = true, BandSoftness = softness, OverrideBands = true, Bands = bands });

            Sweep("Sweep_Surface", new[] { LookSweepSubject.Shapes, LookSweepSubject.Ship },
                new SurfaceStyle?[] { null, driftFaint, driftStrong, brushFaint, brushStrong }
                    .Select(style => new LookSweepVariant { OverrideSurface = true, Surface = style }));

            AssetDatabase.SaveAssets();
            Debug.Log($"[Look] default sweeps in {SweepFolder}, surface styles in {SurfaceFolder}");
        }

        private static T Style<T>(string name, Action<T> configure) where T : SurfaceStyle
        {
            var path = SurfaceFolder + name + ".asset";
            var style = AssetDatabase.LoadAssetAtPath<T>(path);
            if (style != null) return style;
            style = ScriptableObject.CreateInstance<T>();
            configure(style);
            AtmosphereAssetBuilder.EnsureDirectory(SurfaceFolder.TrimEnd('/'));
            AssetDatabase.CreateAsset(style, path);
            return style;
        }

        private static void Sweep(string name, LookSweepSubject[] subjects, IEnumerable<LookSweepVariant> variants)
        {
            var path = SweepFolder + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<LookSweep>(path) != null) return;
            var sweep = ScriptableObject.CreateInstance<LookSweep>();
            sweep.Subjects = subjects;
            sweep.Variants = variants.ToList();
            AtmosphereAssetBuilder.EnsureDirectory(SweepFolder.TrimEnd('/'));
            AssetDatabase.CreateAsset(sweep, path);
        }
    }
}
