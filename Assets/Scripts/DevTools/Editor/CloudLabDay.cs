#nullable enable
using TinCan.Features.Atmosphere;
using TinCan.Features.DayCycle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// The moving day in the cloud lab (<see cref="CloudLab"/>), in edit mode and in play (Play restarts it): while the
    /// lab is the active scene, the
    /// day's clock (<see cref="DayClock"/>, its driver and sun path from <see cref="ConfigPath"/>) runs on the editor's
    /// clock, and each step the sun, its light, the ambient, the air and the painted sky follow the sun's height through
    /// the look's day keys (<see cref="LookProfile.Day"/>, <see cref="DayLightProcessor"/>). Pick Fixed Hour on the config
    /// to stop and scrub. The views repaint only when the light changed. Outside the lab the day's globals are off. Plan: .docs/plans/day-cycle.md.
    /// </summary>
    [InitializeOnLoad]
    public static class CloudLabDay
    {
        public const string ConfigPath = "Assets/Settings/DayCycle/DayCycleConfig.asset";
        private const double StepSeconds = 1.0 / 30.0;

        private static readonly SunPathProcessor SunPath = new();
        private static readonly DayLightProcessor DayLight = new();
        private static readonly AtmosphereProcessor Air = new();
        private static double _start = EditorApplication.timeSinceStartup;
        private static double _lastStep;
        private static float _lastHour = float.NaN;
        private static int _lastEdits;

        static CloudLabDay()
        {
            EditorApplication.update += Step;
            // Opening the lab starts the day at the driver's start.
            EditorSceneManager.sceneOpened += (scene, _) =>
            {
                if (scene.path == CloudLab.ScenePath) _start = EditorApplication.timeSinceStartup;
            };
            EditorApplication.playModeStateChanged += state =>
            {
                bool lab = SceneManager.GetActiveScene().path == CloudLab.ScenePath;
                switch (state)
                {
                    // Another scene's play leaves the light to its own Atmosphere feature.
                    case PlayModeStateChange.ExitingEditMode when !lab:
                        Off();
                        break;
                    // Play in the lab starts the day at the driver's start.
                    case PlayModeStateChange.EnteredPlayMode when lab:
                        Restart();
                        break;
                }
            };
        }

        /// <summary>The hour the lab shows now, NaN before the first step.</summary>
        public static float Hour => _lastHour;

        [MenuItem("TinCan/Dev/Look/Day Cycle/Restart Day")]
        public static void Restart()
        {
            _start = EditorApplication.timeSinceStartup;
            ApplyNow();
        }

        [MenuItem("TinCan/Dev/Look/Day Cycle/Select Config")]
        public static void SelectConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<DayCycleConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogWarning($"[Look] no day config at {ConfigPath}: run TinCan > Dev > Look > Build Atmosphere");
                return;
            }

            Selection.activeObject = config;
        }

        // In edit mode and in play: the lab loads no features, so nothing else owns its light.
        private static bool InLab => SceneManager.GetActiveScene().path == CloudLab.ScenePath;

        private static void Step()
        {
            if (!InLab) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastStep < StepSeconds) return;
            _lastStep = now;

            float before = _lastHour;
            int edits = _lastEdits;
            if (!ApplyNow()) return;
            // Repaint only when the day moved or the config or the look was edited.
            if (!Mathf.Approximately(before, _lastHour) || edits != _lastEdits) InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>Applies the day as the clock has it now; false when the config or the look is missing.</summary>
        public static bool ApplyNow()
        {
            var config = AssetDatabase.LoadAssetAtPath<DayCycleConfig>(ConfigPath);
            if (config == null) return false;
            var clock = new DayClock(config.ActiveDriver, config.Path, SunPath);
            if (!Apply(clock.SunAt(EditorApplication.timeSinceStartup - _start))) return false;
            var look = AssetDatabase.LoadAssetAtPath<LookProfile>(AtmosphereAssetBuilder.LookPath);
            _lastEdits = EditorUtility.GetDirtyCount(config) + (look != null ? EditorUtility.GetDirtyCount(look) : 0);
            return true;
        }

        /// <summary>Applies the light of <paramref name="sun"/> to the open lab: the sun's angle, colour and strength,
        /// the ambient, the air (shader globals and the plain fog) and the sky. False when the look is missing.</summary>
        public static bool Apply(in SunPosition sun)
        {
            var look = AssetDatabase.LoadAssetAtPath<LookProfile>(AtmosphereAssetBuilder.LookPath);
            if (look == null) return false;

            var settings = look.Settings;
            bool hasDay = look.Day.Length > 0;
            var light = hasDay ? DayLight.Evaluate(look.Day, sun.Elevation) : default;
            if (hasDay) settings = DayLight.Apply(settings, light);
            var frame = Air.Evaluate(settings, whiteout: 0f, lightMultiplier: 1f);

            AtmosphereShaderGlobals.Apply(frame, settings);
            AtmosphereShaderGlobals.SetEnabled(true);
            SkyShaderGlobals.SetEnabled(hasDay);
            if (hasDay) SkyShaderGlobals.Apply(light);

            var sunLight = RenderSettings.sun;
            if (sunLight != null)
            {
                sunLight.transform.rotation = sun.LightRotation;
                sunLight.color = frame.SunColour;
                sunLight.intensity = frame.SunIntensity;
            }

            RenderSettings.ambientSkyColor = frame.AmbientSky;
            RenderSettings.ambientEquatorColor = frame.AmbientHorizon;
            RenderSettings.ambientGroundColor = frame.AmbientGround;
            RenderSettings.fogColor = Color.Lerp(frame.AwayFog, frame.SunwardFog, 0.35f);
            RenderSettings.fogDensity = frame.UniformDensityAt(CloudLab.ViewHeight, settings.FogBaseHeight);
            // The sky probe the shaders sample (SampleSH) follows the new ambient.
            DynamicGI.UpdateEnvironment();
            _lastHour = sun.Hour;
            return true;
        }

        /// <summary>The day's globals off: the sky takes its material's colours, nothing darkens.</summary>
        public static void Off()
        {
            SkyShaderGlobals.SetEnabled(false);
            _lastHour = float.NaN;
        }
    }
}
