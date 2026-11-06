#nullable enable
using System;
using TinCan.Core.Audio;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Audio;
using TinCan.Core.RandomStreams;
using TinCan.Core.UI;
using TinCan.Core.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinCan.DevTools.AudioLab
{
    /// <summary>
    /// The audio lab (<c>Assets/Scenes/Lab/Lab_Audio</c>, built by TinCan > Dev > Audio > Build Audio Lab): press Play and
    /// play each channel's sound bites through the game's own <see cref="AudioOneShotPool"/>, shipped
    /// <see cref="AudioConfig"/> and mixer, and move every group's volume. No network, no features: the lab makes its
    /// own pool and UI surface (<see cref="UiLayers"/>, the Dev layer) instead of a project scope. One-shots play at the
    /// emitters (children named in <see cref="Emitters"/>); the engine loop circles the listener, wind and music play in
    /// 2D. Plan: .docs/plans/audio-mixer.md ("The audio lab").
    /// </summary>
    public sealed class AudioLabView : MonoBehaviour
    {
        public const string NearLeft = "Emitter near left";
        public const string MidRight = "Emitter mid right";
        public const string Far = "Emitter far";
        public const string Orbit = "Emitter orbit";
        public static readonly string[] Emitters = { NearLeft, MidRight, Far, Orbit };
        private static readonly string[] Groups = { "Master", "World", "Ui", "Ambience", "Music", "Voice" };

        [SerializeField] private AudioConfig? _config;
        [SerializeField] private PanelSettings? _panelSettings;

        [Header("Sound bites (Assets/Audio/Lab)")]
        [SerializeField] private AudioClip? _uiClick;
        [SerializeField] private AudioClip? _uiConfirm;
        [SerializeField] private AudioClip? _worldThunk;
        [SerializeField] private AudioClip? _worldClang;
        [SerializeField] private AudioClip? _voiceBlip;
        [SerializeField] private AudioClip? _engineLoop;
        [SerializeField] private AudioClip? _windLoop;
        [SerializeField] private AudioClip? _musicLoop;

        [Header("Orbit")]
        [SerializeField] private float _orbitRadius = 12f;
        [SerializeField] private float _orbitSeconds = 10f;

        private readonly VolumeToDecibelsProcessor _decibels = new();
        private AudioOneShotPool? _pool;
        private UiLayers? _layers;
        private AudioSource? _engine;
        private AudioSource? _wind;
        private AudioSource? _music;
        private Transform? _orbit;
        private Vector3 _orbitCentre;
        private Label? _voices;

        /// <summary>Where the orbiting emitter is <paramref name="seconds"/> in: a circle in the ground plane, one lap per <paramref name="lapSeconds"/>.</summary>
        public static Vector3 OrbitPosition(Vector3 centre, float radius, float lapSeconds, float seconds)
        {
            float angle = lapSeconds <= 0f ? 0f : seconds / lapSeconds * 2f * Mathf.PI;
            return centre + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
        }

        private void Start()
        {
            _layers = new UiLayers(_panelSettings, new UiStyleSheets(Array.Empty<StyleSheet>()));
            var card = new Card();
            card.style.position = Position.Absolute;
            card.style.left = card.style.top = 16;
            card.style.width = 400;
            _layers.Root(UiLayer.Dev).Add(card);
            card.Add(Card.Heading("Audio lab"));

            if (_config == null || _config.Mixer == null)
            {
                card.Add(Card.Hint("No AudioConfig with a mixer: rebuild with TinCan > Dev > Audio > Build Audio Lab."));
                return;
            }

            _pool = new AudioOneShotPool(_config, new LabTime(), new RandomSource(null));
            _orbit = transform.Find(Orbit);
            _orbitCentre = _orbit != null ? _orbit.position - OrbitPosition(Vector3.zero, _orbitRadius, _orbitSeconds, 0f) : Vector3.zero;
            _engine = Loop(_orbit != null ? _orbit.gameObject : gameObject, _engineLoop, AudioChannel.World, spatial: true);
            _wind = Loop(gameObject, _windLoop, AudioChannel.Ambience, spatial: false);
            _music = Loop(gameObject, _musicLoop, AudioChannel.Music, spatial: false);
            Build(card, _config);
        }

        private void Update()
        {
            if (_orbit != null) _orbit.position = OrbitPosition(_orbitCentre, _orbitRadius, _orbitSeconds, Time.time);
            if (_voices != null && _config != null) _voices.text = $"One-shot voices playing: {Playing()} of {_config.PoolSize}";
        }

        private void OnDestroy()
        {
            _pool?.Dispose();
            _layers?.Dispose();
            if (_config == null || _config.Mixer == null) return;
            foreach (var group in Groups) _config.Mixer.ClearFloat($"Vol_{group}"); // back to the authored levels
        }

        private void Build(VisualElement card, AudioConfig config)
        {
            card.Add(Card.Hint("World plays in 3D at the spheres (you are the capsule, facing them); the others play in 2D."));

            Section(card, "World");
            card.Add(Row(
                Button("Thunk, near left", () => Play(_worldThunk, AudioChannel.World, NearLeft)),
                Button("Clang, mid right", () => Play(_worldClang, AudioChannel.World, MidRight))));
            card.Add(Row(
                Button("Clang, far", () => Play(_worldClang, AudioChannel.World, Far)),
                Button("Thunk ±10% pitch", () => Play(_worldThunk, AudioChannel.World, NearLeft, 0.1f))));
            card.Add(Button("Burst: 40 clangs (the pool cuts off the oldest)", Burst));
            card.Add(LoopToggle(_engine, "Engine loop, circling you"));

            Section(card, "Ui");
            card.Add(Row(
                Button("Click", () => Play(_uiClick, AudioChannel.Ui, null)),
                Button("Confirm", () => Play(_uiConfirm, AudioChannel.Ui, null))));

            Section(card, "Ambience, Music, Voice");
            card.Add(LoopToggle(_wind, "Wind (Ambience)"));
            card.Add(LoopToggle(_music, "Music loop"));
            card.Add(Button("Voice blip", () => Play(_voiceBlip, AudioChannel.Voice, null)));

            Section(card, "Volumes (mixer groups)");
            foreach (var group in Groups) card.Add(VolumeSlider(config, group));

            _voices = Card.Hint(string.Empty);
            card.Add(_voices);
        }

        private void Play(AudioClip? clip, AudioChannel channel, string? emitter, float pitchJitter = 0f)
        {
            if (_pool == null || clip == null) return;
            Vector3? at = null;
            if (emitter != null)
            {
                var marker = transform.Find(emitter);
                at = marker != null ? marker.position : transform.position;
            }
            _pool.PlayOneShot(clip, channel, at, 1f, pitchJitter);
        }

        private void Burst()
        {
            for (int i = 0; i < 40; i++) Play(_worldClang, AudioChannel.World, Emitters[i % 3], 0.15f);
        }

        private VisualElement VolumeSlider(AudioConfig config, string group)
        {
            var slider = new Slider(group, 0f, 1f) { value = 1f };
            slider.style.flexGrow = 1;
            var readout = new Label("0 dB");
            readout.style.width = 56;
            readout.style.unityTextAlign = TextAnchor.MiddleRight;
            slider.RegisterValueChangedCallback(change =>
            {
                float decibels = _decibels.Decibels(change.newValue);
                config.Mixer!.SetFloat($"Vol_{group}", decibels);
                readout.text = decibels <= VolumeToDecibelsProcessor.Silent ? "off" : $"{decibels:0} dB";
            });
            return Row(slider, readout);
        }

        private int Playing()
        {
            int playing = 0;
            if (_pool == null) return playing;
            foreach (var source in _pool.Sources) if (source.isPlaying) playing++;
            return playing;
        }

        private AudioSource? Loop(GameObject host, AudioClip? clip, AudioChannel channel, bool spatial)
        {
            if (clip == null || _config == null) return null;
            var source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.outputAudioMixerGroup = _config.GroupFor(channel);
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = _config.Rolloff;
            source.minDistance = _config.MinDistance;
            source.maxDistance = _config.MaxDistance;
            source.dopplerLevel = 0f;
            return source;
        }

        private static VisualElement LoopToggle(AudioSource? loop, string label)
        {
            var toggle = new Toggle(label) { value = false };
            toggle.SetEnabled(loop != null);
            toggle.RegisterValueChangedCallback(change =>
            {
                if (loop == null) return;
                if (change.newValue) loop.Play();
                else loop.Stop();
            });
            return toggle;
        }

        private static Button Button(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.flexGrow = 1;
            return button;
        }

        private static VisualElement Row(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            foreach (var child in children) row.Add(child);
            return row;
        }

        private static void Section(VisualElement card, string title)
        {
            var heading = Card.Heading(title);
            heading.style.marginTop = 8;
            card.Add(heading);
        }

        /// <summary>Unity's clock: the lab has no project scope to inject the game's time service.</summary>
        private sealed class LabTime : ITimeService
        {
            public float Time => UnityEngine.Time.time;
            public float DeltaTime => UnityEngine.Time.deltaTime;
            public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
            public int Tick => UnityEngine.Time.frameCount;
            public int TickRate => Math.Max(1, Mathf.RoundToInt(1f / Math.Max(1e-4f, UnityEngine.Time.smoothDeltaTime)));
        }
    }
}
