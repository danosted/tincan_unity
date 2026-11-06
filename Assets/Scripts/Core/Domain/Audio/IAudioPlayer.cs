#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain.Audio
{
    /// <summary>
    /// Plays one-shot sounds through the mixer. Features play sound through this or through a gameplay cue; an
    /// <c>AudioSource</c> in a feature's own prefab is only for loops, with its output group set. A dedicated server and
    /// batch-mode bots get a silent player. Implemented in <c>TinCan.Audio</c>.
    /// </summary>
    public interface IAudioPlayer
    {
        /// <summary>
        /// Plays <paramref name="clip"/> once on <paramref name="channel"/>'s group: in 3D at <paramref name="position"/>,
        /// or in 2D when it is null. <paramref name="pitchJitter"/> varies the pitch by up to that much either way
        /// (0.05 is ±5%), so a repeated sound does not sound identical.
        /// </summary>
        void PlayOneShot(AudioClip clip, AudioChannel channel, Vector3? position, float volume = 1f, float pitchJitter = 0f);
    }
}
