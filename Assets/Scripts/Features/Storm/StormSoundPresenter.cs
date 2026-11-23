#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Every peer with a view, presentation only: the storm's sound. Three looping beds, heard everywhere on the deck
    /// (2D, through the config's mixer group, Ambience): the wind rising with the weight, the rain with it a little
    /// later, and the heart's pulse near the eye. Thunder plays when its time comes (<see cref="Thunder"/>). Driven by
    /// <see cref="StormWeatherPresenter"/>; its game object is made on first use and kept for the scene.
    /// </summary>
    public sealed class StormSoundPresenter : IDisposable
    {
        private readonly StormConfig _config;
        private GameObject? _root;
        private AudioSource? _wind;
        private AudioSource? _rain;
        private AudioSource? _heart;
        private AudioSource? _thunder;
        private float _thunderAt = float.PositiveInfinity;
        private int _thunderSlot;

        public StormSoundPresenter(StormConfig config) => _config = config;

        /// <summary>Schedules a thunderclap at the shared clock's <paramref name="at"/> (a later strike replaces it).</summary>
        public void Thunder(float at, int slot)
        {
            _thunderAt = at;
            _thunderSlot = slot;
        }

        public void Step(float now, float depth, float strength, float weight)
        {
            if (_root == null) Build();

            SetVolume(_wind, _config.WindVolume * Mathf.Sqrt(weight));
            SetVolume(_rain, _config.RainVolume * Mathf.Clamp01(weight * 1.4f - 0.2f));
            SetVolume(_heart, _config.HeartVolume * StormProcessor.HeartShare(depth, strength, _config.HeartFromDepth));

            if (now < _thunderAt) return;
            _thunderAt = float.PositiveInfinity;
            if (_thunder == null || _config.Thunder.Length == 0) return;
            var clip = _config.Thunder[Math.Abs(_thunderSlot) % _config.Thunder.Length];
            if (clip != null) _thunder.PlayOneShot(clip, _config.ThunderVolume * Mathf.Clamp01(0.4f + weight));
        }

        public void Dispose()
        {
            if (_root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
            else UnityEngine.Object.DestroyImmediate(_root);
        }

        private void Build()
        {
            _root = new GameObject("Storm Sound");
            _wind = Bed(_config.WindLoop);
            _rain = Bed(_config.RainLoop);
            _heart = Bed(_config.HeartLoop);
            _thunder = _root.AddComponent<AudioSource>();
            _thunder.playOnAwake = false;
            _thunder.spatialBlend = 0f;
            _thunder.outputAudioMixerGroup = _config.Output;
        }

        private AudioSource? Bed(AudioClip? clip)
        {
            if (clip == null) return null;
            var source = _root!.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.outputAudioMixerGroup = _config.Output;
            source.Play();
            return source;
        }

        private static void SetVolume(AudioSource? source, float volume)
        {
            if (source != null) source.volume = Mathf.Clamp01(volume);
        }
    }
}
