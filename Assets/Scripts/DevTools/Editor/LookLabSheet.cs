#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Features.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Look > Lab Sheet: the look on test geometry, one question per tile, so a choice is made between
    /// pictures rather than words. Rendered like the contact sheet (a preview scene, the golden-afternoon look's sun, air
    /// and post) into <see cref="LookContactSheet.OutputFolder"/>/lab.png, three tiles across, two down:
    /// <list type="number">
    /// <item>Softness: canvas spheres lit from the side (casting no shadow on each other); columns band softness 0.02,
    /// 0.1, 0.3, 0.6 (the library's is 0.35); top row two bands,
    /// bottom row three.</item>
    /// <item>Shapes: a sphere, cylinder, capsule, cube and post on a deck slab with the library's own settings, casting
    /// shadows.</item>
    /// <item>Materials: one sphere per library material (the spirit glows, for the bloom).</item>
    /// <item>Air, away from the sun: dark pillars standing on the cloud sea at 60 m to 3 km.</item>
    /// <item>Air, toward the sun: the same, looking into the light.</item>
    /// <item>Air by height: pillars 250 m off at heights from the cloud sea up to 200 m above it.</item>
    /// </list>
    /// Plan: .docs/plans/look-slice.md.
    /// </summary>
    public static class LookLabSheet
    {
        public const string FileName = "lab.png";
        private const int TileWidth = 640;
        private const int TileHeight = 540;
        private const int Columns = 3;
        private const float ViewHeight = 60f;
        /// <summary>Tiles are built this far apart, so no tile sees another's geometry.</summary>
        private const float TileSpacing = 6000f;

        public static readonly float[] Softness = { 0.02f, 0.1f, 0.3f, 0.6f };

        private enum Tile { Softness, Shapes, Materials, AirAway, AirToward, AirHeights }

        [MenuItem("TinCan/Dev/Look/Lab Sheet")]
        public static void RenderMenu()
        {
            var path = Render();
            Debug.Log($"[Look] lab sheet: {path}");
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>Renders the lab sheet and returns the picture's full path.</summary>
        public static string Render()
        {
            var palette = LookPalette.Load();
            var look = AssetDatabase.LoadAssetAtPath<LookProfile>(AtmosphereAssetBuilder.LookPath)
                ?? throw new InvalidOperationException($"no look at {AtmosphereAssetBuilder.LookPath}: run TinCan > Dev > Look > Build Atmosphere");
            var settings = look.Settings;
            var frame = new AtmosphereProcessor().Evaluate(settings, whiteout: 0f, lightMultiplier: 1f);
            // The direction the sunlight travels, and its heading on the ground.
            var lightTravel = Quaternion.Euler(settings.SunAngles.x, settings.SunAngles.y, 0f) * Vector3.forward;
            float sunHeading = settings.SunAngles.y;

            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var scene = EditorSceneManager.NewPreviewScene();
            var rows = Enum.GetValues(typeof(Tile)).Length / Columns;
            var sheet = new Texture2D(TileWidth * Columns, TileHeight * rows, TextureFormat.RGB24, false);
            // HDR, so glows exceed 1 and the look's bloom picks them up.
            var target = new RenderTexture(TileWidth, TileHeight, 24, RenderTextureFormat.DefaultHDR) { antiAliasing = 4 };
            var temporary = new List<Object>();
            GameObject? volume = null;
            try
            {
                LookContactSheet.AddSun(scene, palette, look);
                var camera = LookContactSheet.AddCamera(scene, palette, look, target);
                camera.farClipPlane = 4000f;
                volume = LookContactSheet.ApplyLook(look);

                foreach (Tile tile in Enum.GetValues(typeof(Tile)))
                {
                    var centre = new Vector3((int)tile * TileSpacing, ViewHeight, 0f);
                    Build(tile, scene, centre, settings, sunHeading, temporary);
                    Aim(camera, tile, centre, sunHeading);
                    camera.backgroundColor = AirColour(frame, settings, camera.transform.forward, lightTravel);
                    // The first render after a script reload can come out blank; a second one is cheap.
                    if (tile == Tile.Softness) camera.Render();
                    camera.Render();

                    RenderTexture.active = target;
                    int column = (int)tile % Columns, row = (int)tile / Columns;
                    // Tiles left to right, top row first; texture rows start at the bottom.
                    sheet.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), column * TileWidth, (rows - 1 - row) * TileHeight);
                    RenderTexture.active = null;
                }

                sheet.Apply();
                var folder = Path.Combine(LookPalette.ProjectRoot, LookContactSheet.OutputFolder);
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, FileName);
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                return path;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                AtmosphereShaderGlobals.SetEnabled(false);
                if (volume != null) Object.DestroyImmediate(volume);
                foreach (var item in temporary) Object.DestroyImmediate(item);
                RenderTexture.active = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(sheet);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Build(Tile tile, Scene scene, Vector3 centre, LookSettings settings, float sunHeading, List<Object> temporary)
        {
            // Across the view, for rows of objects facing a camera that looks along SideHeading.
            var across = Quaternion.Euler(0f, SideHeading(sunHeading), 0f) * Vector3.right;
            switch (tile)
            {
                case Tile.Softness:
                {
                    var canvas = Library("M_ShipCanvas");
                    for (int row = 0; row < 2; row++)
                    for (int column = 0; column < Softness.Length; column++)
                    {
                        var variant = new Material(canvas) { name = $"Canvas soft {Softness[column]} bands {2 + row}" };
                        variant.SetFloat("_BandSoftness", Softness[column]);
                        variant.SetFloat("_Bands", 2 + row);
                        temporary.Add(variant);
                        var position = centre + across * ((column - 1.5f) * 2.6f) + Vector3.up * ((0.5f - row) * 2.6f);
                        Shape(scene, PrimitiveType.Sphere, position, Vector3.one * 2f, variant).shadowCastingMode = ShadowCastingMode.Off;
                    }

                    break;
                }
                case Tile.Shapes:
                {
                    var forward = Vector3.Cross(across, Vector3.up);
                    Shape(scene, PrimitiveType.Cube, centre + Vector3.down * 0.2f, new Vector3(14f, 0.4f, 7f), Library("M_ShipDeck"),
                        Quaternion.LookRotation(forward));
                    var items = new (PrimitiveType Type, string Material, Vector3 Scale, float Lift, Quaternion Turn)[]
                    {
                        (PrimitiveType.Sphere, "M_ShipCanvas", Vector3.one * 2f, 1f, Quaternion.identity),
                        (PrimitiveType.Cylinder, "M_ShipIron", new Vector3(1.6f, 1.2f, 1.6f), 1.2f, Quaternion.identity),
                        (PrimitiveType.Capsule, "M_ShipEngine", new Vector3(1.4f, 1.4f, 1.4f), 1.4f, Quaternion.identity),
                        (PrimitiveType.Cube, "M_ShipHull", Vector3.one * 1.8f, 0.9f, Quaternion.Euler(0f, 45f, 0f)),
                        (PrimitiveType.Cube, "M_ShipWalls", new Vector3(0.6f, 4f, 0.6f), 2f, Quaternion.identity),
                    };
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i];
                        Shape(scene, item.Type, centre + across * ((i - 2f) * 2.6f) + Vector3.up * item.Lift, item.Scale,
                            Library(item.Material), item.Turn);
                    }

                    break;
                }
                case Tile.Materials:
                {
                    var library = LookMaterials.Library;
                    for (int i = 0; i < library.Count; i++)
                    {
                        int row = i < 5 ? 0 : 1, column = i < 5 ? i : i - 5;
                        float shift = row == 0 ? -2f : -1.5f;
                        var position = centre + across * ((column + shift) * 2.5f) + Vector3.up * ((0.5f - row) * 2.5f);
                        Shape(scene, PrimitiveType.Sphere, position, Vector3.one * 2f, Library(library[i].Name));
                    }

                    break;
                }
                case Tile.AirAway:
                case Tile.AirToward:
                {
                    float heading = tile == Tile.AirAway ? sunHeading : sunHeading + 180f;
                    var look = Quaternion.Euler(0f, heading, 0f);
                    float[] distances = { 60f, 120f, 250f, 500f, 1000f, 2000f, 3000f };
                    // Each at its own bearing across the view (degrees), so no pillar hides another.
                    float[] bearings = { -26f, 24f, -14f, 12f, -4f, 4f, 18f };
                    for (int i = 0; i < distances.Length; i++)
                    {
                        float side = Mathf.Tan(bearings[i] * Mathf.Deg2Rad) * distances[i];
                        var foot = centre + look * new Vector3(side, 0f, distances[i]);
                        float width = Mathf.Max(4f, distances[i] * 0.03f);
                        Pillar(scene, foot, settings.FogBaseHeight, ViewHeight + distances[i] * 0.1f, width);
                    }

                    break;
                }
                case Tile.AirHeights:
                {
                    var look = Quaternion.Euler(0f, SideHeading(sunHeading), 0f);
                    float[] heights = { 0f, 25f, 50f, 100f, 200f };
                    for (int i = 0; i < heights.Length; i++)
                    {
                        var foot = centre + look * new Vector3((i - 2f) * 45f, 0f, 250f);
                        float bottom = settings.FogBaseHeight + heights[i];
                        Pillar(scene, foot, bottom, bottom + 30f, 14f);
                    }

                    break;
                }
            }
        }

        private static void Aim(Camera camera, Tile tile, Vector3 centre, float sunHeading)
        {
            float side = SideHeading(sunHeading);
            switch (tile)
            {
                case Tile.Softness:
                case Tile.Materials:
                    camera.fieldOfView = 30f;
                    Place(camera, centre, Quaternion.Euler(0f, side, 0f), 20f);
                    break;
                case Tile.Shapes:
                    camera.fieldOfView = 30f;
                    Place(camera, centre + Vector3.up * 1f, Quaternion.Euler(22f, side, 0f), 30f);
                    break;
                case Tile.AirAway:
                    camera.fieldOfView = 50f;
                    camera.transform.SetPositionAndRotation(centre, Quaternion.Euler(4f, sunHeading, 0f));
                    break;
                case Tile.AirToward:
                    camera.fieldOfView = 50f;
                    camera.transform.SetPositionAndRotation(centre, Quaternion.Euler(4f, sunHeading + 180f, 0f));
                    break;
                case Tile.AirHeights:
                    camera.fieldOfView = 50f;
                    camera.transform.SetPositionAndRotation(centre, Quaternion.Euler(-8f, side, 0f));
                    break;
            }
        }

        /// <summary>A heading at right angles to the sunlight, so objects are lit from the side and show their bands.</summary>
        private static float SideHeading(float sunHeading) => sunHeading - 90f;

        private static void Place(Camera camera, Vector3 target, Quaternion rotation, float distance) =>
            camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);

        /// <summary>The colour the air takes along a view direction, as TinCanAtmosphere.hlsl colours it.</summary>
        private static Color AirColour(AtmosphereFrame frame, LookSettings settings, Vector3 view, Vector3 lightTravel)
        {
            float sunward = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(view, -lightTravel) * 0.5f + 0.5f), settings.SunwardFocus);
            return Color.Lerp(frame.AwayFog, frame.SunwardFog, sunward);
        }

        private static void Pillar(Scene scene, Vector3 foot, float bottom, float top, float width) =>
            _ = Shape(scene, PrimitiveType.Cylinder, new Vector3(foot.x, (bottom + top) * 0.5f, foot.z), new Vector3(width, (top - bottom) * 0.5f, width),
                Library("M_ShipWalls"));

        private static Material Library(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(LookMaterials.Find(name).Path)
            ?? throw new InvalidOperationException($"missing {name}: run TinCan > Dev > Look > Build Materials");

        private static MeshRenderer Shape(Scene scene, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
        {
            var shape = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            SceneManager.MoveGameObjectToScene(shape, scene);
            shape.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            shape.transform.localScale = scale;
            var renderer = shape.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }
    }
}
