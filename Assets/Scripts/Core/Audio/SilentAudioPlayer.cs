#nullable enable
using TinCan.Core.Domain.Audio;
using UnityEngine;

namespace TinCan.Core.Audio
{
    /// <summary>Plays nothing: a dedicated server, batch-mode bots, or a scene with no <see cref="AudioConfig"/>.</summary>
    public sealed class SilentAudioPlayer : IAudioPlayer
    {
        public void PlayOneShot(AudioClip clip, AudioChannel channel, Vector3? position, float volume = 1f, float pitchJitter = 0f)
        {
        }
    }
}
