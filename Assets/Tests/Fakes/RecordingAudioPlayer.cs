#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Audio;
using UnityEngine;

namespace TinCan.Tests.Fakes
{
    /// <summary>Records each one-shot as a readable line: "clip channel 3D|2D volume".</summary>
    public sealed class RecordingAudioPlayer : IAudioPlayer
    {
        public List<string> Played { get; } = new();

        public void PlayOneShot(AudioClip clip, AudioChannel channel, Vector3? position, float volume = 1f, float pitchJitter = 0f) =>
            Played.Add($"{clip.name} {channel} {(position.HasValue ? "3D" : "2D")} {volume}");
    }
}
