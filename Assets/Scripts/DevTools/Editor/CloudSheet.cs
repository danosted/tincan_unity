#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Look > Cloud Sheets: renders the cloud lab (<see cref="CloudLab"/>, opened or built first) through
    /// its own camera into pictures in <see cref="LookContactSheet.OutputFolder"/>, so cloud choices are made between
    /// pictures:
    /// <list type="bullet">
    /// <item><see cref="StepsFile"/>: what each step of the soft look adds, left to right: before (the banded materials on
    /// a flat colour), + the sky, + soft light (the soft materials as they are, the chosen look), + creases and mottling
    /// (<see cref="StepCrease"/>, <see cref="StepMottle"/>). Rows: the hero view, the middle cumulus close up, from high
    /// above.</item>
    /// <item><see cref="SeaFile"/>: the sea approaches (<see cref="CloudLab.Seas"/>, A to D) left to right; top row from
    /// the hero view, bottom row from high above.</item>
    /// <item><see cref="BetweenFile"/>: between the banded cumulus (the developer liked its clear contours) and the soft
    /// one, on the middle cumulus close up: columns blend every value of the material from banded to soft
    /// (<see cref="BetweenBlend"/>), rows light it from the front, the side, and ahead and to the side
    /// (<see cref="BetweenLight"/>).</item>
    /// <item><see cref="CumulusFile"/>: the cumulus styles (<see cref="CloudLab.CumulusStyles"/>: dome, first lobes, heap)
    /// left to right; rows the hero view, the middle cumulus close up, from high above.</item>
    /// <item><see cref="SweepFile"/>: the soft cumulus material, 3 by 3, on the middle cumulus close up: rows crease
    /// (<see cref="SweepCrease"/>), columns shape blend (<see cref="SweepShapeBlend"/>), the centre tile the default.</item>
    /// </list>
    /// The scene is left as it was. Plan: .docs/plans/look-slice.md ("The cloud lab").
    /// </summary>
    public static class CloudSheet
    {
        public const string StepsFile = "cloud-steps.png";
        public const string SeaFile = "cloud-seas.png";
        public const string SweepFile = "cloud-sweep.png";
        public const string CumulusFile = "cloud-cumulus.png";
        public const string BetweenFile = "cloud-between.png";
        private const int TileWidth = 640;
        private const int TileHeight = 360;
        private const float HighViewHeight = 260f;
        private const float HighViewPitch = 22f;
        /// <summary>How far the close-up turns from straight broadside, so the lit side, the shadow side and the creases
        /// between lobes all show (from the hero view the cumulus is backlit).</summary>
        private const float CloseUpTurn = 25f;

        /// <summary>The creases and mottling the steps sheet's last column adds to the soft materials (which have none).</summary>
        public const float StepCrease = 0.5f;
        public const float StepMottle = 0.08f;

        public static readonly float[] SweepCrease = { 0.3f, 0.6f, 0.9f };
        public static readonly float[] SweepShapeBlend = { 0.3f, 0.5f, 0.7f };
        /// <summary>How far each column of the between sheet goes from the banded cumulus material (0) to the soft one (1).</summary>
        public static readonly float[] BetweenBlend = { 0f, 1f / 3f, 2f / 3f, 1f };
        /// <summary>The light on each row of the between sheet, in degrees round from the way the sunlight travels: from
        /// the front, from the side, and ahead and to the side (as in the hero view, where the banded clouds read
        /// best).</summary>
        public static readonly float[] BetweenLight = { 0f, 90f, 135f };

        private enum View { Hero, CloseUp, High }

        private sealed class Lab
        {
            public Lab(Camera camera, Transform seas, List<MeshRenderer> clouds, MeshRenderer middle)
            {
                Camera = camera;
                Seas = seas;
                Clouds = clouds;
                Middle = middle;
                Position = camera.transform.position;
                Rotation = camera.transform.rotation;
                FieldOfView = camera.fieldOfView;
                ClearFlags = camera.clearFlags;
            }

            public Camera Camera { get; }
            public Transform Seas { get; }
            public List<MeshRenderer> Clouds { get; }
            public MeshRenderer Middle { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public float FieldOfView { get; }
            public CameraClearFlags ClearFlags { get; }
        }

        [MenuItem("TinCan/Dev/Look/Cloud Sheets")]
        public static void RenderMenu()
        {
            if (SceneManager.GetActiveScene().path != CloudLab.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (File.Exists(Path.Combine(LookPalette.ProjectRoot, CloudLab.ScenePath))) EditorSceneManager.OpenScene(CloudLab.ScenePath);
                else CloudLab.Build();
            }

            var paths = Render();
            Debug.Log($"[Look] cloud sheets: {string.Join(", ", paths)}");
            EditorUtility.RevealInFinder(paths[0]);
        }

        /// <summary>Renders every sheet from the open cloud lab and returns their full paths.</summary>
        public static string[] Render()
        {
            var camera = GameObject.Find("Lab Camera")?.GetComponent<Camera>()
                ?? throw new InvalidOperationException($"no Lab Camera: open {CloudLab.ScenePath} (TinCan > Dev > Look > Build Cloud Lab)");
            var seas = GameObject.Find($"Clouds (new)/{CloudLab.SeaRoot}")?.transform
                ?? throw new InvalidOperationException($"no sea in {CloudLab.ScenePath}: rebuild it");
            var clouds = GameObject.Find("Clouds (new)")!.GetComponentsInChildren<MeshRenderer>(includeInactive: true).ToList();
            var middle = clouds.FirstOrDefault(r => r.name == "Cumulus middle")
                ?? throw new InvalidOperationException("no Cumulus middle in the lab: rebuild it");
            var lab = new Lab(camera, seas, clouds, middle);

            var seasActive = seas.Cast<Transform>().ToDictionary(t => t, t => t.gameObject.activeSelf);
            var materials = clouds.ToDictionary(r => r, r => r.sharedMaterial);
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            // HDR, so glows exceed 1 and the look's bloom picks them up.
            var target = new RenderTexture(TileWidth, TileHeight, 24, RenderTextureFormat.DefaultHDR) { antiAliasing = 4 };
            var temporary = new List<Object>();
            try
            {
                camera.targetTexture = target;
                var folder = Path.Combine(LookPalette.ProjectRoot, LookContactSheet.OutputFolder);
                Directory.CreateDirectory(folder);
                return new[]
                {
                    Steps(lab, materials, target, temporary, folder),
                    SeaApproaches(lab, seasActive, target, temporary, folder),
                    Sweep(lab, materials, target, temporary, folder),
                    Cumulus(lab, target, temporary, folder),
                    Between(lab, materials, target, temporary, folder),
                };
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                camera.targetTexture = null;
                camera.transform.SetPositionAndRotation(lab.Position, lab.Rotation);
                camera.fieldOfView = lab.FieldOfView;
                camera.clearFlags = lab.ClearFlags;
                foreach (var (sea, active) in seasActive) sea.gameObject.SetActive(active);
                foreach (var (renderer, material) in materials) renderer.sharedMaterial = material;
                RenderTexture.active = null;
                Object.DestroyImmediate(target);
                foreach (var item in temporary) Object.DestroyImmediate(item);
            }
        }

        private static string Steps(Lab lab, Dictionary<MeshRenderer, Material> materials, RenderTexture target, List<Object> temporary, string folder)
        {
            var banded = new Dictionary<string, Material>
            {
                [CloudLab.SoftCumulus] = LoadMaterial(CloudLab.BandedCumulus),
                [CloudLab.SoftSea] = LoadMaterial(CloudLab.BandedSea),
            };
            var creased = new Dictionary<string, Material>();
            foreach (var name in banded.Keys)
            {
                var variant = new Material(LoadMaterial(name)) { name = name + " (creases and mottling)" };
                variant.SetFloat("_Crease", StepCrease);
                variant.SetFloat("_Mottle", StepMottle);
                temporary.Add(variant);
                creased[name] = variant;
            }

            // Each step: the materials it swaps in (by the soft material they replace), and whether the sky shows.
            var steps = new (Dictionary<string, Material>? Swap, bool Sky)[] { (banded, false), (banded, true), (null, true), (creased, true) };
            var views = new[] { View.Hero, View.CloseUp, View.High };
            var sheet = Sheet(steps.Length, views.Length, temporary);
            for (int column = 0; column < steps.Length; column++)
            {
                foreach (var (renderer, material) in materials)
                    renderer.sharedMaterial = steps[column].Swap != null && steps[column].Swap!.TryGetValue(material.name, out var swap) ? swap : material;
                lab.Camera.clearFlags = steps[column].Sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                for (int row = 0; row < views.Length; row++)
                {
                    Aim(lab, views[row]);
                    Tile(lab.Camera, target, sheet, column, row, views.Length);
                }
            }

            foreach (var (renderer, material) in materials) renderer.sharedMaterial = material;
            lab.Camera.clearFlags = lab.ClearFlags;
            return Write(sheet, folder, StepsFile);
        }

        private static string SeaApproaches(Lab lab, Dictionary<Transform, bool> seasActive, RenderTexture target, List<Object> temporary, string folder)
        {
            var sheet = Sheet(CloudLab.Seas.Length, 2, temporary);
            for (int column = 0; column < CloudLab.Seas.Length; column++)
            {
                foreach (Transform sea in lab.Seas) sea.gameObject.SetActive(sea.name == CloudLab.Seas[column]);
                Aim(lab, View.Hero);
                Tile(lab.Camera, target, sheet, column, 0, 2);
                Aim(lab, View.High);
                Tile(lab.Camera, target, sheet, column, 1, 2);
            }

            foreach (var (sea, active) in seasActive) sea.gameObject.SetActive(active);
            return Write(sheet, folder, SeaFile);
        }

        private static string Sweep(Lab lab, Dictionary<MeshRenderer, Material> materials, RenderTexture target, List<Object> temporary, string folder)
        {
            var soft = LoadMaterial(CloudLab.SoftCumulus);
            var cumulus = materials.Where(pair => pair.Value == soft).Select(pair => pair.Key).ToList();
            Aim(lab, View.CloseUp);
            var sheet = Sheet(SweepShapeBlend.Length, SweepCrease.Length, temporary);
            for (int row = 0; row < SweepCrease.Length; row++)
            for (int column = 0; column < SweepShapeBlend.Length; column++)
            {
                var variant = new Material(soft) { name = $"Cumulus crease {SweepCrease[row]} shape {SweepShapeBlend[column]}" };
                variant.SetFloat("_Crease", SweepCrease[row]);
                variant.SetFloat("_ShapeBlend", SweepShapeBlend[column]);
                temporary.Add(variant);
                foreach (var renderer in cumulus) renderer.sharedMaterial = variant;
                Tile(lab.Camera, target, sheet, column, row, SweepCrease.Length);
            }

            foreach (var renderer in cumulus) renderer.sharedMaterial = materials[renderer];
            return Write(sheet, folder, SweepFile);
        }

        private static string Between(Lab lab, Dictionary<MeshRenderer, Material> materials, RenderTexture target, List<Object> temporary, string folder)
        {
            var banded = LoadMaterial(CloudLab.BandedCumulus);
            var soft = LoadMaterial(CloudLab.SoftCumulus);
            var cumulus = materials.Where(pair => pair.Value == soft).Select(pair => pair.Key).ToList();
            var sheet = Sheet(BetweenBlend.Length, BetweenLight.Length, temporary);
            for (int row = 0; row < BetweenLight.Length; row++)
            for (int column = 0; column < BetweenBlend.Length; column++)
            {
                Aim(lab, View.CloseUp, sunTurn: BetweenLight[row]);
                var variant = Blend(banded, soft, BetweenBlend[column]);
                variant.name = $"Cumulus between {BetweenBlend[column]:0.00} light {BetweenLight[row]}";
                temporary.Add(variant);
                foreach (var renderer in cumulus) renderer.sharedMaterial = variant;
                Tile(lab.Camera, target, sheet, column, row, BetweenLight.Length);
            }

            foreach (var renderer in cumulus) renderer.sharedMaterial = materials[renderer];
            return Write(sheet, folder, BetweenFile);
        }

        /// <summary>A material whose every number and colour lies <paramref name="t"/> of the way from one to the other.</summary>
        private static Material Blend(Material from, Material to, float t)
        {
            var blend = new Material(from);
            var shader = from.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                int id = shader.GetPropertyNameId(i);
                switch (shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        blend.SetFloat(id, Mathf.Lerp(from.GetFloat(id), to.GetFloat(id), t));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        blend.SetColor(id, Color.Lerp(from.GetColor(id), to.GetColor(id), t));
                        break;
                }
            }

            return blend;
        }

        private static string Cumulus(Lab lab, RenderTexture target, List<Object> temporary, string folder)
        {
            var root = GameObject.Find("Clouds (new)")!.transform;
            var styles = CloudLab.CumulusStyles.Select(s => root.Find(s.Root)).ToArray();
            var middles = CloudLab.CumulusStyles.Select(s => "Cumulus middle" + s.Suffix).ToArray();
            var active = styles.Select(t => t.gameObject.activeSelf).ToArray();
            var views = new[] { View.Hero, View.CloseUp, View.High };
            var sheet = Sheet(styles.Length, views.Length, temporary);
            try
            {
                for (int column = 0; column < styles.Length; column++)
                {
                    for (int i = 0; i < styles.Length; i++) styles[i].gameObject.SetActive(i == column);
                    var middle = lab.Clouds.First(r => r.name == middles[column]);
                    for (int row = 0; row < views.Length; row++)
                    {
                        Aim(lab, views[row], middle);
                        Tile(lab.Camera, target, sheet, column, row, views.Length);
                    }
                }
            }
            finally
            {
                for (int i = 0; i < styles.Length; i++) styles[i].gameObject.SetActive(active[i]);
            }

            return Write(sheet, folder, CumulusFile);
        }

        /// <summary>Points the camera. A close-up with <paramref name="sunTurn"/> looks that many degrees round from the way
        /// the sunlight travels (0 the sun behind the camera, 90 from the side, 135 ahead and to the side, as in the hero
        /// view); without it, broadside to the cumulus.</summary>
        private static void Aim(Lab lab, View view, MeshRenderer? subject = null, float? sunTurn = null)
        {
            var camera = lab.Camera;
            camera.fieldOfView = lab.FieldOfView;
            switch (view)
            {
                case View.Hero:
                    camera.transform.SetPositionAndRotation(lab.Position, lab.Rotation);
                    break;
                case View.High:
                    camera.transform.SetPositionAndRotation(new Vector3(lab.Position.x, HighViewHeight, lab.Position.z),
                        Quaternion.Euler(HighViewPitch, lab.Rotation.eulerAngles.y, 0f));
                    break;
                case View.CloseUp:
                {
                    var middle = subject ?? lab.Middle;
                    var bounds = middle.bounds;
                    // Broadside to the cumulus (its lobes run along its local x), from the side the sun lights, turned a
                    // little so the shadow side shows too.
                    var across = middle.transform.forward;
                    var light = RenderSettings.sun != null ? RenderSettings.sun.transform.forward : lab.Rotation * Vector3.forward;
                    if (Vector3.Dot(across, light) < 0f) across = -across;
                    var forward = Quaternion.Euler(0f, CloseUpTurn, 0f) * new Vector3(across.x, 0f, across.z).normalized;
                    if (sunTurn != null)
                        forward = Quaternion.Euler(0f, Quaternion.LookRotation(light).eulerAngles.y + sunTurn.Value, 0f) * Vector3.forward;
                    float distance = bounds.extents.magnitude * 2.4f;
                    var eye = bounds.center - forward * distance;
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(bounds.center - eye));
                    camera.fieldOfView = 2f * Mathf.Atan(bounds.extents.magnitude * 0.9f / distance) * Mathf.Rad2Deg;
                    break;
                }
            }
        }

        private static Material LoadMaterial(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(CloudLab.MaterialFolder + name + ".mat")
            ?? throw new InvalidOperationException($"no {name}: rebuild the cloud lab");

        private static Texture2D Sheet(int columns, int rows, List<Object> temporary)
        {
            var sheet = new Texture2D(TileWidth * columns, TileHeight * rows, TextureFormat.RGB24, false);
            temporary.Add(sheet);
            return sheet;
        }

        private static void Tile(Camera camera, RenderTexture target, Texture2D sheet, int column, int row, int rows)
        {
            // The first render after a change can come out blank; a second one is cheap.
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            // Tiles left to right, top row first; texture rows start at the bottom.
            sheet.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), column * TileWidth, (rows - 1 - row) * TileHeight);
            RenderTexture.active = null;
        }

        private static string Write(Texture2D sheet, string folder, string file)
        {
            sheet.Apply();
            var path = Path.Combine(folder, file);
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            return path;
        }
    }
}
