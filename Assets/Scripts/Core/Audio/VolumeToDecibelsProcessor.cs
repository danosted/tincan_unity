#nullable enable
using System;

namespace TinCan.Core.Audio
{
    /// <summary>
    /// A volume slider's 0..1 to a mixer level in dB, on the log curve ears hear as even steps (half the slider is
    /// -6 dB). 0 is <see cref="Silent"/>, the mixer's floor; 1 is 0 dB, the level as authored.
    /// </summary>
    public sealed class VolumeToDecibelsProcessor
    {
        public const float Silent = -80f;

        public float Decibels(float volume)
        {
            if (float.IsNaN(volume) || volume <= 0f) return Silent;
            return Math.Max(Silent, 20f * (float)Math.Log10(Math.Min(1f, volume)));
        }
    }
}
