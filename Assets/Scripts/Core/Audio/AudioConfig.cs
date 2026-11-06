#nullable enable
using TinCan.Core.Domain.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace TinCan.Core.Audio
{
    /// <summary>
    /// The mixer, the group each <see cref="AudioChannel"/> plays through, the one-shot pool's size and the one rolloff
    /// every 3D sound uses (decision 2 in .docs/plans/audio-mixer.md). The shipped asset is <c>Assets/Audio/AudioConfig</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "TinCan/Audio/Audio Config")]
    public sealed class AudioConfig : ScriptableObject
    {
        public AudioMixer? Mixer;

        [Header("Groups")]
        public AudioMixerGroup? World;
        public AudioMixerGroup? Ui;
        public AudioMixerGroup? Ambience;
        public AudioMixerGroup? Music;
        public AudioMixerGroup? Voice;

        [Header("One-shot pool")]
        [Tooltip("One-shots that can play at once; the oldest is cut off when a new one needs a voice.")]
        [Min(1)] public int PoolSize = 24;

        [Header("3D rolloff (every World sound)")]
        public AudioRolloffMode Rolloff = AudioRolloffMode.Logarithmic;
        [Min(0.01f)] public float MinDistance = 2f;
        [Min(0.01f)] public float MaxDistance = 60f;

        /// <summary>The group <paramref name="channel"/> plays through, or null when it is not set (plays unrouted).</summary>
        public AudioMixerGroup? GroupFor(AudioChannel channel) => channel switch
        {
            AudioChannel.World => World,
            AudioChannel.Ui => Ui,
            AudioChannel.Ambience => Ambience,
            AudioChannel.Music => Music,
            AudioChannel.Voice => Voice,
            _ => null,
        };
    }
}
