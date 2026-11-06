#nullable enable
using System;

namespace TinCan.Core.Audio
{
    /// <summary>
    /// Picks the voice a new one-shot plays on, for a fixed number of voices: the first one that has finished, else the
    /// one that started longest ago (it is cut off). Pure, so the pool's choice is tested without audio.
    /// </summary>
    public sealed class AudioVoiceProcessor
    {
        private readonly float[] _startedAt;
        private readonly float[] _endsAt;

        public AudioVoiceProcessor(int voices)
        {
            if (voices < 1) throw new ArgumentOutOfRangeException(nameof(voices), voices, "A pool needs at least one voice.");
            _startedAt = new float[voices];
            _endsAt = new float[voices];
            for (int i = 0; i < voices; i++) _startedAt[i] = _endsAt[i] = float.NegativeInfinity;
        }

        public int Voices => _endsAt.Length;

        /// <summary>Takes a voice at <paramref name="now"/> for <paramref name="seconds"/> and returns its index.</summary>
        public int Take(float now, float seconds)
        {
            int voice = Pick(now);
            _startedAt[voice] = now;
            _endsAt[voice] = now + Math.Max(0f, seconds);
            return voice;
        }

        /// <summary>True while the voice's sound is still playing at <paramref name="now"/>.</summary>
        public bool IsBusy(int voice, float now) => _endsAt[voice] > now;

        private int Pick(float now)
        {
            int oldest = 0;
            for (int i = 0; i < _endsAt.Length; i++)
            {
                if (_endsAt[i] <= now) return i;
                if (_startedAt[i] < _startedAt[oldest]) oldest = i;
            }
            return oldest;
        }
    }
}
