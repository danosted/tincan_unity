#nullable enable
using UnityEngine.Audio;

namespace TinCan.Core.Audio
{
    /// <summary>
    /// The mixer's exposed levels in dB, behind an interface so <see cref="AudioVolumeBinder"/> is tested without a mixer
    /// (Unity has no public API for making one in code).
    /// </summary>
    public interface IAudioMixerLevels
    {
        /// <summary>The level now: the authored one until something sets it.</summary>
        bool TryGetLevel(string parameter, out float decibels);
        void SetLevel(string parameter, float decibels);

        /// <summary>Back to the authored level.</summary>
        void ClearLevel(string parameter);
    }

    /// <summary><see cref="IAudioMixerLevels"/> over a real <see cref="AudioMixer"/>.</summary>
    public sealed class AudioMixerLevels : IAudioMixerLevels
    {
        private readonly AudioMixer _mixer;

        public AudioMixerLevels(AudioMixer mixer)
        {
            _mixer = mixer;
        }

        public bool TryGetLevel(string parameter, out float decibels) => _mixer.GetFloat(parameter, out decibels);
        public void SetLevel(string parameter, float decibels) => _mixer.SetFloat(parameter, decibels);
        public void ClearLevel(string parameter) => _mixer.ClearFloat(parameter);
    }
}
