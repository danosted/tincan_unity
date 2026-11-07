#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Settings;
using VContainer.Unity;

namespace TinCan.Core.Audio
{
    /// <summary>
    /// The Audio settings page's volumes, applied to the mixer's exposed <c>Vol_*</c> levels. A volume of 1 is the
    /// authored level, lower follows <see cref="VolumeToDecibelsProcessor"/>'s curve below it, and 0 is silent. Levels
    /// are applied on a tick, never at construction: Unity ignores <c>AudioMixer.SetFloat</c> during <c>Awake</c>. A
    /// change applies on the next tick, so a dragged slider sets the mixer once a frame. Without a settings service, or
    /// for a key no loaded definition has, the mixer keeps its authored level. When the scope ends the levels are cleared
    /// back to the authored ones (in the Editor an override would outlive Play mode).
    /// </summary>
    public sealed class AudioVolumeBinder : ITickable, IDisposable
    {
        public const string MasterKey = "audio.master";
        public const string WorldKey = "audio.world";
        public const string UiKey = "audio.ui";
        public const string AmbienceKey = "audio.ambience";
        public const string MusicKey = "audio.music";

        /// <summary>Each volume setting and the mixer level it drives.</summary>
        public static readonly IReadOnlyList<(string Key, string Parameter)> Volumes = new[]
        {
            (MasterKey, "Vol_Master"),
            (WorldKey, "Vol_World"),
            (UiKey, "Vol_Ui"),
            (AmbienceKey, "Vol_Ambience"),
            (MusicKey, "Vol_Music"),
        };

        private readonly IAudioMixerLevels _mixer;
        private readonly IPlayerSettings? _settings;
        private readonly VolumeToDecibelsProcessor _decibels = new();
        private readonly Dictionary<string, float> _authored = new();
        private bool _pending;

        public AudioVolumeBinder(IAudioMixerLevels mixer, IPlayerSettings? settings)
        {
            _mixer = mixer;
            _settings = settings;
            if (_settings == null) return;

            _settings.Changed += OnSettingChanged;
            _pending = true;
        }

        public void Tick()
        {
            if (!_pending || _settings == null) return;
            _pending = false;

            foreach (var (key, parameter) in Volumes)
            {
                if (!_settings.Has(key)) continue;

                if (!_authored.TryGetValue(parameter, out float authored))
                {
                    authored = _mixer.TryGetLevel(parameter, out float level) ? level : 0f;
                    _authored[parameter] = authored;
                }

                _mixer.SetLevel(parameter, Level(authored, _settings.GetFloat(key)));
            }
        }

        /// <summary>The mixer level for <paramref name="volume"/> on a group authored at <paramref name="authored"/> dB.</summary>
        public float Level(float authored, float volume)
        {
            float below = _decibels.Decibels(volume);
            if (below <= VolumeToDecibelsProcessor.Silent) return VolumeToDecibelsProcessor.Silent;
            return Math.Max(VolumeToDecibelsProcessor.Silent, authored + below);
        }

        public void Dispose()
        {
            if (_settings != null) _settings.Changed -= OnSettingChanged;
            foreach (var parameter in _authored.Keys) _mixer.ClearLevel(parameter);
            _authored.Clear();
        }

        private void OnSettingChanged(string key)
        {
            foreach (var (volumeKey, _) in Volumes)
            {
                if (volumeKey != key) continue;
                _pending = true;
                return;
            }
        }
    }
}
