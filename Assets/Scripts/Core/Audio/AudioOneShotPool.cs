#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Audio;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace TinCan.Core.Audio
{
    /// <summary>
    /// <see cref="IAudioPlayer"/> over a fixed pool of <c>AudioSource</c>s under one root, each routed to its channel's
    /// group when it plays. Built on the first sound; when every voice is busy the oldest is cut off
    /// (<see cref="AudioVoiceProcessor"/>). Nothing is allocated per sound.
    /// </summary>
    public sealed class AudioOneShotPool : IAudioPlayer, IDisposable
    {
        private readonly AudioConfig _config;
        private readonly ITimeService _time;
        private readonly System.Random _random;
        private readonly AudioVoiceProcessor _voices;
        private AudioSource[]? _sources;
        private GameObject? _root;

        [Inject]
        public AudioOneShotPool(AudioConfig config, ITimeService time, IRandomSource random)
        {
            _config = config;
            _time = time;
            _random = random.Create("Audio");
            _voices = new AudioVoiceProcessor(Mathf.Max(1, config.PoolSize));
        }

        /// <summary>The pool's sources, built on the first sound (empty before). For tests.</summary>
        public AudioSource[] Sources => _sources ?? Array.Empty<AudioSource>();

        public void PlayOneShot(AudioClip clip, AudioChannel channel, Vector3? position, float volume = 1f, float pitchJitter = 0f)
        {
            if (clip == null) return;

            float pitch = 1f + Jitter(pitchJitter);
            float now = _time.Time;
            var source = Build()[_voices.Take(now, clip.length / pitch)];

            source.outputAudioMixerGroup = _config.GroupFor(channel);
            source.spatialBlend = position.HasValue ? 1f : 0f;
            source.transform.position = position ?? Vector3.zero;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.Play();
        }

        public void Dispose()
        {
            if (_root == null) return;
            if (Application.isPlaying) Object.Destroy(_root);
            else Object.DestroyImmediate(_root);
            _root = null;
            _sources = null;
        }

        private float Jitter(float amount)
        {
            float jitter = Mathf.Clamp(amount, 0f, 0.9f);
            return jitter <= 0f ? 0f : ((float)_random.NextDouble() * 2f - 1f) * jitter;
        }

        private AudioSource[] Build()
        {
            if (_sources != null && _root != null) return _sources;

            _root = new GameObject("AudioOneShotPool");
            if (Application.isPlaying) Object.DontDestroyOnLoad(_root);
            _sources = new AudioSource[_voices.Voices];
            for (int i = 0; i < _sources.Length; i++)
            {
                var voice = new GameObject($"Voice{i}");
                voice.transform.SetParent(_root.transform, worldPositionStays: false);
                var source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.rolloffMode = _config.Rolloff;
                source.minDistance = _config.MinDistance;
                source.maxDistance = Mathf.Max(_config.MinDistance, _config.MaxDistance);
                source.dopplerLevel = 0f; // the ship moves under every sound; doppler would bend them all
                _sources[i] = source;
            }
            return _sources;
        }
    }
}
