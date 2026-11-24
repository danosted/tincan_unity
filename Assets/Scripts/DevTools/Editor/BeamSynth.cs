#nullable enable
using System;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Stand-ins for the spirit beam's sounds until recordings exist (plan: .docs/plans/ray-cannons.md, R3). Mono at
    /// <see cref="AudioLabSynth.SampleRate"/>, samples in -1..1.
    /// <list type="bullet">
    /// <item><see cref="Pulse"/>: one throb of the held beam, a soft, bright thrum; played once per pulse, so a held
    /// beam hums in its rhythm. Shorter than <c>CannonConfig.PulseSeconds</c> plus a tail, so pulses overlap a little.</item>
    /// <item><see cref="Vent"/>: the overheated barrel venting, a breathy hiss falling away under a low, sinking tone.</item>
    /// </list>
    /// </summary>
    public static class BeamSynth
    {
        private const float Tau = 2f * MathF.PI;

        public static float[] Pulse()
        {
            const float seconds = 0.22f;
            int rate = AudioLabSynth.SampleRate;
            var samples = new float[(int)(seconds * rate)];
            var random = new Random(11);
            float phase = 0f;
            float noise = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float attack = MathF.Min(1f, t / 0.006f);
                // A tone gliding down a little as it fades, with an octave and a fifth over it.
                phase += Tau * (520f - 120f * MathF.Min(1f, t / 0.08f)) / rate;
                float tone = MathF.Sin(phase) + 0.45f * MathF.Sin(2f * phase) + 0.2f * MathF.Sin(3f * phase);
                noise = noise * 0.85f + 0.15f * (float)(random.NextDouble() * 2.0 - 1.0);
                samples[i] = attack * (MathF.Exp(-t * 22f) * tone + MathF.Exp(-t * 60f) * 1.2f * noise);
            }
            return Normalize(samples, 0.6f);
        }

        public static float[] Vent()
        {
            const float seconds = 1.1f;
            int rate = AudioLabSynth.SampleRate;
            var samples = new float[(int)(seconds * rate)];
            var random = new Random(23);
            float phase = 0f;
            float low = 0f;
            float band = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float attack = MathF.Min(1f, t / 0.02f);
                // Noise, band-passed by two one-pole filters: a hiss without the harsh top.
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low = low * 0.55f + 0.45f * white;
                band = band * 0.97f + 0.03f * low;
                float hiss = low - band;
                phase += Tau * (180f - 90f * t / seconds) / rate;
                samples[i] = attack * (MathF.Exp(-t * 2.8f) * 1.4f * hiss + MathF.Exp(-t * 4f) * 0.35f * MathF.Sin(phase));
            }
            return Normalize(samples, 0.7f);
        }

        private static float[] Normalize(float[] samples, float to)
        {
            float peak = 1e-4f;
            foreach (float sample in samples) peak = MathF.Max(peak, MathF.Abs(sample));
            for (int i = 0; i < samples.Length; i++) samples[i] *= to / peak;
            return samples;
        }
    }
}
