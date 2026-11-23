#nullable enable
using System;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Stand-in storm sounds for the storm lab, synthesized so the layering and the mix can be tuned before real
    /// recordings exist (plan: .docs/plans/storm-crossing.md, ST1). Each is mono at <see cref="AudioLabSynth.SampleRate"/>;
    /// the loops run on into their start. Swap in real clips on the StormConfig without touching code.
    /// </summary>
    public static class StormSynth
    {
        private const int Rate = AudioLabSynth.SampleRate;
        private const float Tau = 2f * MathF.PI;

        /// <summary>Twelve seconds of storm wind: low, heavy noise in long surges with a howl riding the gusts.</summary>
        public static float[] Wind()
        {
            const float length = 12f;
            const float fade = 1.5f;
            var random = new Random(23);
            var raw = new float[Samples(length + fade)];
            float low = 0f, band = 0f, howl = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / Rate;
                float surge = 0.55f + 0.3f * MathF.Sin(Tau * t / length) + 0.15f * MathF.Sin(Tau * 4f * t / length + 2f);
                float cutoff = 0.004f + 0.03f * surge;
                low += cutoff * ((float)random.NextDouble() * 2f - 1f - low);
                band += cutoff * (low - band);
                howl = 0.15f * surge * surge * MathF.Sin(Tau * (180f + 60f * surge) * t);
                raw[i] = surge * (low - band) * 9f + howl * Math.Abs(low) * 6f;
            }
            return Normalize(AudioLabSynth.CrossfadeLoop(raw, Samples(length), Samples(fade)), 0.7f);
        }

        /// <summary>Six seconds of rain: bright hiss with scattered drops ticking on wood and canvas.</summary>
        public static float[] Rain()
        {
            const float length = 6f;
            const float fade = 0.5f;
            var random = new Random(5);
            var raw = new float[Samples(length + fade)];
            float last = 0f, drop = 0f, dropTone = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float noise = (float)random.NextDouble() * 2f - 1f;
                float hiss = noise - last; // a first difference: the noise's highs
                last = noise;
                if (random.NextDouble() < 0.0009) { drop = 0.6f + 0.4f * (float)random.NextDouble(); dropTone = 900f + 1400f * (float)random.NextDouble(); }
                drop *= 0.9985f;
                float t = (float)i / Rate;
                raw[i] = 0.35f * hiss + drop * MathF.Sin(Tau * dropTone * t) * 0.5f;
            }
            return Normalize(AudioLabSynth.CrossfadeLoop(raw, Samples(length), Samples(fade)), 0.55f);
        }

        /// <summary>
        /// A thunderclap: a sharp crack (bright noise, quick to die), then a long low rumble in rolling swells. Variants
        /// by <paramref name="seed"/>: near ones crack harder, far ones mostly rumble.
        /// </summary>
        public static float[] Thunder(int seed)
        {
            const float length = 6f;
            var random = new Random(seed);
            var samples = new float[Samples(length)];
            float crack = 0.4f + 0.6f * (float)random.NextDouble();
            float swells = 2f + 3f * (float)random.NextDouble();
            float low = 0f, lower = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += 0.02f * (noise - low);
                lower += 0.01f * (low - lower);
                float roll = 0.6f + 0.4f * MathF.Sin(Tau * swells * t / length + seed);
                float rumble = MathF.Min(1f, t / 0.15f) * MathF.Exp(-t * 0.55f) * roll * lower * 30f;
                float snap = crack * MathF.Exp(-t * 14f) * noise;
                samples[i] = rumble + snap;
            }
            return Normalize(samples, 0.9f);
        }

        /// <summary>The heart's pulse: a deep double beat (lub-dub) every 1.6 seconds over a low drone (a whole number of cycles in the loop), looping.</summary>
        public static float[] Heart()
        {
            const float length = 3.2f;
            const float beat = 1.6f;
            var samples = new float[Samples(length)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / Rate;
                float local = t % beat;
                float thump = Pulse(local, 0f) + 0.7f * Pulse(local, 0.28f);
                float drone = 0.25f * MathF.Sin(Tau * 41.25f * t) * (0.8f + 0.2f * MathF.Sin(Tau * t / length));
                samples[i] = thump * MathF.Sin(Tau * 52f * local) + drone;
            }
            return Normalize(samples, 0.75f);
        }

        private static float Pulse(float t, float at)
        {
            float local = t - at;
            return local < 0f ? 0f : MathF.Min(1f, local / 0.01f) * MathF.Exp(-local * 9f);
        }

        private static int Samples(float seconds) => (int)MathF.Round(seconds * Rate);

        private static float[] Normalize(float[] samples, float peak)
        {
            float max = 0f;
            foreach (var s in samples) max = MathF.Max(max, MathF.Abs(s));
            if (max <= 0f) return samples;
            float scale = peak / max;
            for (int i = 0; i < samples.Length; i++) samples[i] *= scale;
            return samples;
        }
    }
}
