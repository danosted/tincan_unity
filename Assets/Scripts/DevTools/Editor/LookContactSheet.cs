#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Look > Contact Sheet: builds a built-in ship design in edit mode, in a preview scene of its own (so
    /// the open scene's lights and objects stay out of it), and renders four fixed views (side, three-quarter, bow, deck
    /// at eye height) into one picture in <see cref="OutputFolder"/>. Agents and people review the same picture after
    /// every look change. The light is a fixed late-afternoon sun from the palette until the look profile owns it.
    /// <see cref="Render"/> takes any built-in design by name. Plan: .docs/plans/look-slice.md (L0).
    /// </summary>
    public static class LookContactSheet
    {
        public const string OutputFolder = "Logs/look/";
        public const string DefaultDesign = ShipFrameDesigns.CorsairName;
        private const int TileWidth = 960;
        private const int TileHeight = 540;
        private const float FieldOfView = 30f;
        private const float DeckFieldOfView = 60f;
        private const float EyeHeight = 1.7f;

        private enum View { Side, ThreeQuarter, Bow, Deck }

        [MenuItem("TinCan/Dev/Look/Contact Sheet")]
        public static void RenderDefault()
        {
            var path = Render(DefaultDesign);
            Debug.Log($"[Look] contact sheet: {path}");
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>Renders the contact sheet of a built-in design and returns the picture's full path.</summary>
        public static string Render(string designName)
        {
            var config = AssetDatabase.LoadAssetAtPath<ShipDesignsConfig>(ShipDesignsAssetBuilder.ConfigPath)
                ?? throw new InvalidOperationException($"no ship designs config at {ShipDesignsAssetBuilder.ConfigPath}");
            var design = BuiltInDesign(config, designName);
            var palette = LookPalette.Load();

            // Compile shader variants on the spot: asynchronous compilation would leave the first views blank.
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var scene = EditorSceneManager.NewPreviewScene();
            var sheet = new Texture2D(TileWidth * 2, TileHeight * 2, TextureFormat.RGB24, false);
            var target = new RenderTexture(TileWidth, TileHeight, 24) { antiAliasing = 4 };
            try
            {
                var root = new GameObject(designName);
                SceneManager.MoveGameObjectToScene(root, scene);
                new ShipHullAssembler(Catalog(), new ShipyardPreviewBuilder(), buildFunctionalParts: true, config).Apply(design, root.transform);

                AddSun(scene, palette);
                var camera = AddCamera(scene, palette, target);
                var corners = RendererCorners(root);
                var deck = DeckEye(design.Frame);

                foreach (View view in Enum.GetValues(typeof(View)))
                {
                    Aim(camera, view, corners, deck);
                    camera.Render();
                    RenderTexture.active = target;
                    int tile = (int)view;
                    // Tiles left to right, top row first; texture rows start at the bottom.
                    sheet.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), tile % 2 * TileWidth, (1 - tile / 2) * TileHeight);
                    RenderTexture.active = null;
                }

                sheet.Apply();
                var folder = Path.Combine(LookPalette.ProjectRoot, OutputFolder);
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, $"{designName}-contact.png");
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                return path;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                RenderTexture.active = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(sheet);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static ShipDesign BuiltInDesign(ShipDesignsConfig config, string name)
        {
            var codec = new ShipDesignJsonCodec(config.Limits);
            foreach (var asset in config.BuiltInDesigns.Where(a => a != null))
            {
                var decoded = codec.Decode(asset.text);
                if (decoded.Design?.Name == name) return decoded.Design;
            }

            throw new ArgumentException($"no built-in ship design named '{name}' in {ShipDesignsAssetBuilder.ConfigPath}");
        }

        private static ShipPartCatalog Catalog() =>
            new(AssetDatabase.FindAssets("t:FeatureInstaller", new[] { "Assets/Resources/Installers" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FeatureInstaller>(AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<FeatureInstaller.IExtension<ShipPartDefinition>>()
                .SelectMany(e => e.Contributions));

        private static void AddSun(Scene scene, LookPalette palette)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(sun.gameObject, scene);
            sun.type = LightType.Directional;
            sun.color = Color.Lerp(Color.white, palette["leaf-gold"], 0.35f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            // Low and from the port quarter, so the side and three-quarter views see lit and shadowed faces.
            sun.transform.rotation = Quaternion.Euler(30f, 60f, 0f);
        }

        private static Camera AddCamera(Scene scene, LookPalette palette, RenderTexture target)
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene;
            camera.cameraType = CameraType.Game;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = palette["sky"];
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.targetTexture = target;
            return camera;
        }

        /// <summary>The corners of every renderer's bounds: a tighter outline of the ship than one box around it all.</summary>
        private static List<Vector3> RendererCorners(GameObject root)
        {
            var corners = new List<Vector3>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var b = renderer.bounds;
                for (int i = 0; i < 8; i++)
                    corners.Add(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            }

            if (corners.Count == 0) corners.Add(root.transform.position);
            return corners;
        }

        /// <summary>Where a crew member stands on the main deck: the story with the most plank floor, at eye height,
        /// forward of the middle (so the view aft takes in the waist, the masts and the castle), nudged to starboard off
        /// the centreline where masts stand.</summary>
        private static Vector3 DeckEye(ShipFrame frame)
        {
            var floors = frame.Faces
                .Where(f => f.Fill == ShipFrameFill.Planks)
                .Select(f => f.Nodes.Select(id => frame.TryGetNode(id, out var n) ? n : default).ToList())
                .Where(nodes => nodes.Count >= 3 && nodes.All(n => n.Story == nodes[0].Story))
                .Select(nodes => (Story: nodes[0].Story, Loop: nodes.Select(frame.Position).ToList()))
                .ToList();
            if (floors.Count == 0) return Vector3.up * EyeHeight;

            var deck = floors.GroupBy(f => f.Story).OrderByDescending(g => g.Sum(f => ShipFrameGeometry.AreaXZ(f.Loop))).First();
            var points = deck.SelectMany(f => f.Loop).ToList();
            float x = points.Average(p => p.x);
            float z = Mathf.Lerp(points.Min(p => p.z), points.Max(p => p.z), 0.6f);
            return new Vector3(x + 1.2f, ShipFrameUnits.ToMetres(frame.StoryFloor(deck.Key)) + EyeHeight, z);
        }

        private static void Aim(Camera camera, View view, IReadOnlyList<Vector3> corners, Vector3 deckEye)
        {
            if (view == View.Deck)
            {
                camera.fieldOfView = DeckFieldOfView;
                camera.transform.position = deckEye;
                // Aft, along the deck toward the masts and the castle.
                camera.transform.rotation = Quaternion.LookRotation(Vector3.back + Vector3.down * 0.05f);
                return;
            }

            camera.fieldOfView = FieldOfView;
            var from = (view switch
            {
                View.Side => new Vector3(-1f, 0.05f, 0f),
                View.ThreeQuarter => new Vector3(-1f, 0.65f, -1f),
                _ => new Vector3(-0.6f, 0.35f, 1f),
            }).normalized;
            camera.transform.rotation = Quaternion.LookRotation(-from);
            Fit(camera, corners, from);
        }

        /// <summary>Moves the camera back along <paramref name="from"/> until every point is in view (with a small
        /// margin), centred on the points as the camera sees them.</summary>
        private static void Fit(Camera camera, IReadOnlyList<Vector3> points, Vector3 from)
        {
            float tanV = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * TileWidth / TileHeight;
            var right = camera.transform.right;
            var up = camera.transform.up;

            var mean = points.Aggregate(Vector3.zero, (sum, p) => sum + p) / points.Count;
            float minX = points.Min(p => Vector3.Dot(p - mean, right)), maxX = points.Max(p => Vector3.Dot(p - mean, right));
            float minY = points.Min(p => Vector3.Dot(p - mean, up)), maxY = points.Max(p => Vector3.Dot(p - mean, up));
            var centre = mean + right * ((minX + maxX) * 0.5f) + up * ((minY + maxY) * 0.5f);

            float distance = points.Max(p =>
            {
                var offset = p - centre;
                float toward = Vector3.Dot(offset, from);
                return Mathf.Max(toward + Mathf.Abs(Vector3.Dot(offset, right)) / tanH, toward + Mathf.Abs(Vector3.Dot(offset, up)) / tanV);
            });
            camera.transform.position = centre + from * (distance * 1.06f);
        }
    }
}
