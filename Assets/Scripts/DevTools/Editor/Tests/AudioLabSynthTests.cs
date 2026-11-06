#nullable enable
using System;
using NUnit.Framework;

namespace TinCan.DevTools.Editor.Tests
{
    /// <summary>Every sound bite is audible and in range, and the loops wrap without a click.</summary>
    public class AudioLabSynthTests
    {
        private static readonly (string Name, Func<float[]> Make, bool Loops)[] Clips =
        {
            ("UiClick", AudioLabSynth.UiClick, false),
            ("UiConfirm", AudioLabSynth.UiConfirm, false),
            ("WorldThunk", AudioLabSynth.WorldThunk, false),
            ("WorldClang", AudioLabSynth.WorldClang, false),
            ("VoiceBlip", AudioLabSynth.VoiceBlip, false),
            ("EngineLoop", AudioLabSynth.EngineLoop, true),
            ("WindLoop", AudioLabSynth.WindLoop, true),
            ("MusicLoop", AudioLabSynth.MusicLoop, true),
        };

        [Test]
        public void EveryClip_IsAudibleAndInRange()
        {
            foreach (var clip in Clips)
            {
                float peak = 0f;
                foreach (var s in clip.Make()) peak = MathF.Max(peak, MathF.Abs(s));
                Assert.That(peak, Is.InRange(0.3f, 1f), clip.Name);
            }
        }

        [Test]
        public void Loops_WrapWithoutAJump()
        {
            foreach (var clip in Clips)
            {
                if (!clip.Loops) continue;
                var samples = clip.Make();
                float wrap = MathF.Abs(samples[0] - samples[^1]);
                Assert.That(wrap, Is.LessThan(0.08f), $"{clip.Name} jumps by {wrap} at the loop point");
            }
        }

        [Test]
        public void WrapLoop_FoldsTheTailOntoTheStart()
        {
            var loop = AudioLabSynth.WrapLoop(new[] { 1f, 2f, 3f, 4f, 5f }, 3);

            Assert.That(loop, Is.EqualTo(new[] { 5f, 7f, 3f }));
        }

        [Test]
        public void Wav_HasAHeaderAndTwoBytesASample()
        {
            var bytes = AudioLabSynth.Wav(new[] { 0f, 1f, -1f });

            Assert.That(bytes.Length, Is.EqualTo(44 + 6));
            Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(BitConverter.ToInt16(bytes, 46), Is.EqualTo(short.MaxValue));
        }
    }
}
