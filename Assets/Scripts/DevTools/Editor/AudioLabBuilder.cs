#nullable enable
using System;
using System.IO;
using TinCan.Core.Audio;
using TinCan.DevTools.AudioLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Audio > Build Audio Lab: writes the lab's sound bites (<see cref="AudioLabSynth"/>) to
    /// <see cref="ClipFolder"/> and builds <see cref="ScenePath"/>, a bare scene (no network, no features) with a
    /// listener at the centre, four emitters around it (near left, mid right, far ahead, one circling) and the
    /// <see cref="AudioLabView"/> panel. Press Play to use it. Rebuilding overwrites both. Plan: .docs/plans/audio-mixer.md.
    /// </summary>
    public static class AudioLabBuilder
    {
        public const string ScenePath = "Assets/Scenes/Lab/Lab_Audio.unity";
        public const string ClipFolder = "Assets/Audio/Lab/";
        public const string ConfigPath = "Assets/Audio/AudioConfig.asset";
        public const string PanelSettingsPath = "Assets/Settings/UI/DefaultPanelSettings.asset";
        private static readonly Vector3 Ear = new(0f, 1.7f, 0f);

        [MenuItem("TinCan/Dev/Audio/Build Audio Lab")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            Debug.Log($"[Audio] audio lab: {ScenePath} (press Play)");
        }

        public static void Build()
        {
            var clips = new (string Field, string Name, Func<float[]> Make)[]
            {
                ("_uiClick", "lab_ui_click", AudioLabSynth.UiClick),
                ("_uiConfirm", "lab_ui_confirm", AudioLabSynth.UiConfirm),
                ("_worldThunk", "lab_world_thunk", AudioLabSynth.WorldThunk),
                ("_worldClang", "lab_world_clang", AudioLabSynth.WorldClang),
                ("_voiceBlip", "lab_voice_blip", AudioLabSynth.VoiceBlip),
                ("_engineLoop", "lab_world_engine_loop", AudioLabSynth.EngineLoop),
                ("_windLoop", "lab_ambience_wind_loop", AudioLabSynth.WindLoop),
                ("_musicLoop", "lab_music_loop", AudioLabSynth.MusicLoop),
            };
            Directory.CreateDirectory(ClipFolder);
            foreach (var clip in clips) File.WriteAllBytes($"{ClipFolder}{clip.Name}.wav", AudioLabSynth.Wav(clip.Make()));
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load assets only after NewScene: opening a scene unloads unused assets, so an earlier reference is dead.
            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigPath)
                ?? throw new InvalidOperationException($"no audio config at {ConfigPath}");

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camera = new GameObject("Lab Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 9f, -10f);
            camera.transform.LookAt(new Vector3(0f, 0f, 14f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.78f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            ground.transform.position = new Vector3(0f, 0f, 20f);

            // The listener stands where the emitters are measured from, facing +Z: near left is on your left.
            var listener = Marker(PrimitiveType.Capsule, "Listener (you)", Ear - Vector3.up * 0.7f, 0.8f, null);
            listener.AddComponent<AudioListener>();

            var lab = new GameObject("Audio Lab");
            Marker(PrimitiveType.Sphere, AudioLabView.NearLeft, Ear + new Vector3(-4f, 0f, 5f), 0.6f, lab.transform);
            Marker(PrimitiveType.Sphere, AudioLabView.MidRight, Ear + new Vector3(10f, 0f, 18f), 0.8f, lab.transform);
            Marker(PrimitiveType.Sphere, AudioLabView.Far, Ear + new Vector3(0f, 0f, 55f), 1.2f, lab.transform);
            Marker(PrimitiveType.Sphere, AudioLabView.Orbit, Ear + new Vector3(0f, 0f, 12f), 0.6f, lab.transform);

            var view = lab.AddComponent<AudioLabView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_config").objectReferenceValue = config;
            serialized.FindProperty("_panelSettings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(PanelSettingsPath)
                ?? throw new InvalidOperationException($"no panel settings at {PanelSettingsPath}");
            foreach (var clip in clips)
            {
                serialized.FindProperty(clip.Field).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipFolder}{clip.Name}.wav")
                    ?? throw new InvalidOperationException($"{clip.Name}.wav did not import");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static GameObject Marker(PrimitiveType shape, string name, Vector3 position, float size, Transform? parent)
        {
            var marker = GameObject.CreatePrimitive(shape);
            marker.name = name;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * size;
            return marker;
        }
    }
}
