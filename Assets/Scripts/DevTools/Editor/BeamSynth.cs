#nullable enable
using System;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// A stand-in for the spirit beam's sound until a recording exists (plan: .docs/plans/ray-cannons.md, R1): a hum that
    /// rises over the charge, then a bright, soft chord as the beam is let go, fading out. Mono at
    /// <see cref="AudioLabSynth.SampleRate"/>, samples in -1..1. Its charge matches <c>CannonConfig.ChargeSeconds</c>.
    /// </summary>
    public static class BeamSynth
    {
        private const float Tau = 2f * MathF.PI;

        public static float[] SpiritBeam(float chargeSeconds = 0.4f, float releaseSeconds = 0.9f)
        {
            int rate = AudioLabSynth.SampleRate;
            var samples = new float[(int)((chargeSeconds + releaseSeconds) * rate)];
            var random = new Random(11);
            float humPhase = 0f;
            float noise = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float value;
                if (t < chargeSeconds)
                {
                    // The charge: a hum gliding up an octave and a half, swelling.
                    float share = t / chargeSeconds;
                    humPhase += Tau * (220f + 440f * share * share) / rate;
                    value = share * share * (0.6f * MathF.Sin(humPhase) + 0.2f * MathF.Sin(2f * humPhase));
                }
                else
                {
                    // The release: a soft chord and a breath of noise, both fading.
                    float since = t - chargeSeconds;
                    float attack = MathF.Min(1f, since / 0.01f);
                    float chord = MathF.Sin(Tau * 660f * since) + 0.6f * MathF.Sin(Tau * 990f * since) + 0.35f * MathF.Sin(Tau * 1320f * since);
                    noise = noise * 0.9f + 0.1f * (float)(random.NextDouble() * 2.0 - 1.0);
                    value = attack * (MathF.Exp(-since * 5f) * 0.5f * chord + MathF.Exp(-since * 9f) * 1.5f * noise);
                    // The hum carries on briefly under the chord, so the two join without a click.
                    humPhase += Tau * 660f / rate;
                    value += MathF.Exp(-since * 30f) * 0.6f * MathF.Sin(humPhase);
                }
                samples[i] = value;
            }

            float peak = 1e-4f;
            foreach (float sample in samples) peak = MathF.Max(peak, MathF.Abs(sample));
            for (int i = 0; i < samples.Length; i++) samples[i] *= 0.7f / peak;
            return samples;
        }
    }
}
