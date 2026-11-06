#nullable enable
using System;
using System.IO;
using System.Text;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// The audio lab's sound bites, made from sine waves and noise so the lab needs no recorded audio: one or two per
    /// channel, and three loops that wrap without a click. Placeholders for testing the mixer, not game sounds. Mono,
    /// <see cref="SampleRate"/> Hz, samples in -1..1.
    /// </summary>
    public static class AudioLabSynth
    {
        public const int SampleRate = 44100;
        private const float Tau = 2f * MathF.PI;

        /// <summary>Ui: a short bright tick.</summary>
        public static float[] UiClick() => Normalize(Render(0.05f, t =>
            Attack(t, 0.002f) * MathF.Exp(-t * 90f) * (MathF.Sin(Tau * 2400f * t) + 0.3f * MathF.Sin(Tau * 4800f * t))), 0.7f);

        /// <summary>Ui: two rising notes.</summary>
        public static float[] UiConfirm() => Normalize(Render(0.3f, t =>
            Note(t, 0f, 660f, 14f) + Note(t, 0.09f, 990f, 10f)), 0.7f);

        /// <summary>World: a low thump with a falling pitch and a burst of noise, like something heavy set down on a deck.</summary>
        public static float[] WorldThunk()
        {
            var random = new Random(7);
            float phase = 0f;
            float noise = 0f;
            var samples = new float[Samples(0.45f)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                phase += Tau * (45f + 75f * MathF.Exp(-t * 18f)) / SampleRate;
                noise += 0.08f * ((float)random.NextDouble() * 2f - 1f - noise); // a one-pole low-pass
                samples[i] = Attack(t, 0.002f) * (MathF.Exp(-t * 9f) * MathF.Sin(phase) + 2.5f * MathF.Exp(-t * 40f) * noise);
            }
            return Normalize(samples, 0.8f);
        }

        /// <summary>World: a struck metal plate (inharmonic partials, the high ones dying first).</summary>
        public static float[] WorldClang()
        {
            float[] frequencies = { 420f, 1094f, 1789f, 2593f, 3412f };
            float[] amplitudes = { 1f, 0.6f, 0.45f, 0.3f, 0.2f };
            float[] decays = { 3f, 4.5f, 6f, 8f, 10f };
            return Normalize(Render(1.4f, t =>
            {
                float sum = 0f;
                for (int k = 0; k < frequencies.Length; k++) sum += amplitudes[k] * MathF.Exp(-t * decays[k]) * MathF.Sin(Tau * frequencies[k] * t);
                return Attack(t, 0.001f) * sum;
            }), 0.8f);
        }

        /// <summary>Voice: a hummed "hm?" (a pitch that rises then falls, shaped by two vowel formants).</summary>
        public static float[] VoiceBlip()
        {
            const float length = 0.55f;
            float phase = 0f;
            var samples = new float[Samples(length)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                float u = t / length;
                float pitch = 150f + 45f * MathF.Sin(MathF.PI * MathF.Min(1f, u * 1.4f)) + 3f * MathF.Sin(Tau * 5f * t);
                phase += Tau * pitch / SampleRate;
                float sum = 0f;
                for (int k = 1; k <= 30; k++)
                {
                    float f = k * pitch;
                    float gain = Formant(f, 700f, 130f) + 0.7f * Formant(f, 1200f, 150f) + 0.25f * Formant(f, 2600f, 250f);
                    sum += gain * MathF.Sin(k * phase) / k;
                }
                samples[i] = Attack(t, 0.03f) * Release(t, length, 0.08f) * sum;
            }
            return Normalize(samples, 0.7f);
        }

        /// <summary>World loop: a two-second engine drone (55 Hz and its harmonics, a slow throb); every partial completes whole cycles, so it loops.</summary>
        public static float[] EngineLoop() => Normalize(Render(2f, t =>
        {
            float sum = 0f;
            for (int k = 1; k <= 8; k++) sum += MathF.Sin(Tau * 55f * k * t + k) / k;
            return (0.8f + 0.2f * MathF.Sin(Tau * 2f * t)) * sum;
        }), 0.6f);

        /// <summary>Ambience loop: eight seconds of gusting wind (filtered noise), its end crossfaded into its start.</summary>
        public static float[] WindLoop()
        {
            const float length = 8f;
            const float fade = 1f;
            var random = new Random(11);
            var raw = new float[Samples(length + fade)];
            float low = 0f;
            float band = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / SampleRate;
                float gust = 0.5f + 0.3f * MathF.Sin(Tau * t / length) + 0.2f * MathF.Sin(Tau * 3f * t / length + 1f);
                float cutoff = 0.01f + 0.05f * gust;
                low += cutoff * ((float)random.NextDouble() * 2f - 1f - low);
                band += cutoff * (low - band);
                raw[i] = gust * (low - band) * 6f;
            }
            return Normalize(CrossfadeLoop(raw, Samples(length), Samples(fade)), 0.6f);
        }

        /// <summary>Music loop: eight seconds at 120 bpm (A minor, F, C, G as eighth-note arpeggios over a soft bass); tails that run past the end wrap to the start.</summary>
        public static float[] MusicLoop()
        {
            const float length = 8f;
            const float beat = 0.5f;
            float[][] chords =
            {
                new[] { 220.00f, 261.63f, 329.63f, 440.00f }, // A minor
                new[] { 174.61f, 220.00f, 261.63f, 349.23f }, // F
                new[] { 196.00f, 261.63f, 329.63f, 392.00f }, // C (over G)
                new[] { 196.00f, 246.94f, 293.66f, 392.00f }, // G
            };
            var raw = new float[Samples(length + 1.5f)];
            for (int chord = 0; chord < chords.Length; chord++)
            {
                float chordStart = chord * 4f * beat;
                AddNote(raw, chordStart, chords[chord][0] / 2f, 4f * beat, 0.5f, 1.2f);
                for (int eighth = 0; eighth < 8; eighth++)
                {
                    int[] order = { 0, 1, 2, 3, 2, 1, 2, 3 };
                    AddNote(raw, chordStart + eighth * beat / 2f, chords[chord][order[eighth]], 0.6f, 0.35f, 6f);
                }
            }
            return Normalize(WrapLoop(raw, Samples(length)), 0.7f);
        }

        /// <summary>16-bit PCM mono WAV bytes.</summary>
        public static byte[] Wav(float[] samples)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.ASCII);
            int bytes = samples.Length * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + bytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(bytes);
            foreach (var sample in samples) writer.Write((short)MathF.Round(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
            writer.Flush();
            return stream.ToArray();
        }

        /// <summary>Folds <paramref name="raw"/>'s samples past <paramref name="length"/> back onto its start, so a decaying tail carries over the loop point.</summary>
        public static float[] WrapLoop(float[] raw, int length)
        {
            var loop = new float[length];
            for (int i = 0; i < raw.Length; i++) loop[i % length] += raw[i];
            return loop;
        }

        /// <summary>A loop of <paramref name="length"/> samples whose start fades in from <paramref name="raw"/>'s overrun, so the end runs on into the start.</summary>
        public static float[] CrossfadeLoop(float[] raw, int length, int fade)
        {
            var loop = new float[length];
            Array.Copy(raw, loop, length);
            for (int i = 0; i < fade && length + i < raw.Length; i++)
            {
                float w = (float)i / fade;
                loop[i] = raw[i] * w + raw[length + i] * (1f - w);
            }
            return loop;
        }

        private static void AddNote(float[] samples, float start, float frequency, float seconds, float gain, float decay)
        {
            int first = Samples(start);
            int count = Math.Min(samples.Length - first, Samples(seconds + 1f));
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Attack(t, 0.005f) * MathF.Exp(-t * decay) * (t < seconds ? 1f : MathF.Exp(-(t - seconds) * 12f));
                samples[first + i] += gain * envelope * (MathF.Sin(Tau * frequency * t) + 0.3f * MathF.Sin(Tau * 2f * frequency * t));
            }
        }

        private static float Note(float t, float start, float frequency, float decay)
        {
            float local = t - start;
            if (local < 0f) return 0f;
            return Attack(local, 0.003f) * MathF.Exp(-local * decay) * (MathF.Sin(Tau * frequency * local) + 0.25f * MathF.Sin(Tau * 3f * frequency * local));
        }

        private static float Formant(float frequency, float centre, float width)
        {
            float d = (frequency - centre) / width;
            return MathF.Exp(-0.5f * d * d);
        }

        private static float Attack(float t, float seconds) => t >= seconds ? 1f : t / seconds;

        private static float Release(float t, float length, float seconds) => t <= length - seconds ? 1f : MathF.Max(0f, (length - t) / seconds);

        private static int Samples(float seconds) => (int)MathF.Round(seconds * SampleRate);

        private static float[] Render(float seconds, Func<float, float> wave)
        {
            var samples = new float[Samples(seconds)];
            for (int i = 0; i < samples.Length; i++) samples[i] = wave((float)i / SampleRate);
            return samples;
        }

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
