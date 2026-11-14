#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinCan.Features.DayCycle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Look > Day Sheet: the cloud lab (<see cref="CloudLab"/>, opened or built first) through the day,
    /// to compare the current (volumetric) clouds with the new (stylized mesh) ones under the moving sun. Columns are
    /// moments of the day from the config's sun path (<see cref="Moments"/>: midnight, the morning blue hour, sunrise,
    /// morning, noon, sunset, the evening blue hour), rows the volumetric clouds alone, the stylized clouds alone (both
    /// from the hero view) and the view away from the sun with both (the belt of Venus side). Written to
    /// <see cref="File"/> in <see cref="LookContactSheet.OutputFolder"/>; the scene and the running day are left as they
    /// were. Plan: .docs/plans/day-cycle.md.
    /// </summary>
    public static class DaySheet
    {
        public const string File = "day.png";
        public const string CurrentClouds = "Clouds (current, volumetric)";
        public const string NewClouds = "Clouds (new)";
        /// <summary>Extra renders before a volumetric tile: the clouds build up over frames.</summary>
        private const int SettleRenders = 8;

        private static readonly SunPathProcessor SunPath = new();

        private readonly struct Row
        {
            public Row(string name, bool current, bool stylized, bool away)
            {
                Name = name;
                Current = current;
                Stylized = stylized;
                Away = away;
            }

            public string Name { get; }
            public bool Current { get; }
            public bool Stylized { get; }
            public bool Away { get; }
        }

        private static readonly Row[] Rows =
        {
            new("volumetric", current: true, stylized: false, away: false),
            new("stylized", current: false, stylized: true, away: false),
            new("away from the sun, both", current: true, stylized: true, away: true),
        };

        [MenuItem("TinCan/Dev/Look/Day Sheet")]
        public static void RenderMenu()
        {
            if (SceneManager.GetActiveScene().path != CloudLab.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (System.IO.File.Exists(Path.Combine(LookPalette.ProjectRoot, CloudLab.ScenePath))) EditorSceneManager.OpenScene(CloudLab.ScenePath);
                else CloudLab.Build();
            }

            var path = Render();
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>The moments the sheet shows, from the sun's path: those the sun never reaches that day are left out.</summary>
        public static IReadOnlyList<(string Name, float Hour)> Moments(in SunPath path)
        {
            float noon = SunPath.NoonElevation(path);
            var moments = new (string Name, float? Hour)[]
            {
                ("midnight", 0f),
                ("blue hour (morning)", SunPath.HourAtElevation(path, -6f, morning: true)),
                ("sunrise", SunPath.HourAtElevation(path, 0.5f, morning: true)),
                ("morning", SunPath.HourAtElevation(path, noon * 0.5f, morning: true)),
                ("noon", 12f),
                ("sunset", SunPath.HourAtElevation(path, 0.5f, morning: false)),
                ("blue hour (evening)", SunPath.HourAtElevation(path, -6f, morning: false)),
            };
            return moments.Where(m => m.Hour != null).Select(m => (m.Name, m.Hour!.Value)).ToList();
        }

        /// <summary>Renders the sheet from the open cloud lab and returns its full path.</summary>
        public static string Render()
        {
            var config = AssetDatabase.LoadAssetAtPath<DayCycleConfig>(CloudLabDay.ConfigPath)
                ?? throw new InvalidOperationException($"no day config at {CloudLabDay.ConfigPath}: run TinCan > Dev > Look > Build Atmosphere");
            var camera = GameObject.Find("Lab Camera")?.GetComponent<Camera>()
                ?? throw new InvalidOperationException($"no Lab Camera: open {CloudLab.ScenePath} (TinCan > Dev > Look > Build Cloud Lab)");
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            var current = roots.FirstOrDefault(r => r.name == CurrentClouds)
                ?? throw new InvalidOperationException($"no {CurrentClouds} in the lab: rebuild it");
            var stylized = roots.FirstOrDefault(r => r.name == NewClouds)
                ?? throw new InvalidOperationException($"no {NewClouds} in the lab: rebuild it");

            var moments = Moments(config.Path);
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            bool currentActive = current.activeSelf, stylizedActive = stylized.activeSelf;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var target = new RenderTexture(CloudSheet.TileWidth, CloudSheet.TileHeight, 24, RenderTextureFormat.DefaultHDR) { antiAliasing = 4 };
            var temporary = new List<Object>();
            try
            {
                camera.targetTexture = target;
                var sheet = CloudSheet.Sheet(moments.Count, Rows.Length, temporary);
                for (int column = 0; column < moments.Count; column++)
                {
                    CloudLabDay.Apply(SunPath.Evaluate(config.Path, moments[column].Hour));
                    for (int row = 0; row < Rows.Length; row++)
                    {
                        current.SetActive(Rows[row].Current);
                        stylized.SetActive(Rows[row].Stylized);
                        camera.transform.SetPositionAndRotation(position, Rows[row].Away ? Quaternion.Euler(0f, 180f, 0f) * rotation : rotation);
                        if (Rows[row].Current)
                        {
                            for (int i = 0; i < SettleRenders; i++) camera.Render();
                        }

                        CloudSheet.Tile(camera, target, sheet, column, row, Rows.Length);
                    }
                }

                var folder = Path.Combine(LookPalette.ProjectRoot, LookContactSheet.OutputFolder);
                Directory.CreateDirectory(folder);
                var file = CloudSheet.Write(sheet, folder, File);
                Debug.Log($"[Look] day sheet: {file}\ncolumns: {string.Join(", ", moments.Select(m => $"{m.Name} {m.Hour:0.0}h"))}\n" +
                          $"rows: {string.Join(", ", Rows.Select(r => r.Name))}");
                return file;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                camera.targetTexture = null;
                camera.transform.SetPositionAndRotation(position, rotation);
                current.SetActive(currentActive);
                stylized.SetActive(stylizedActive);
                RenderTexture.active = null;
                Object.DestroyImmediate(target);
                foreach (var item in temporary) Object.DestroyImmediate(item);
                CloudLabDay.ApplyNow();
            }
        }
    }
}
