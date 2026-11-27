#nullable enable
using NUnit.Framework;
using TinCan.Core.Humanoid;
using UnityEngine;

namespace TinCan.Core.Humanoid.Tests
{
    /// <summary>
    /// The drawn body must move evenly at any display rate. A 30 Hz tick fires inside whichever frame crosses the tick
    /// boundary, so with VSync the ticks land 4 or 5 frames apart on a 144 Hz screen (4.8 frames per tick). Here a body
    /// walks at a constant speed while ticks and frames are paced as NGO and VSync pace them. Even motion means the drawn
    /// body trails the true one by a constant lag; judder is that lag wobbling, shown as frozen or doubled frames.
    /// </summary>
    public class HumanoidVisualFramePacingTests
    {
        private const int TickRate = 30;
        private const float Speed = 5f; // m/s, about a walk
        private const double Seconds = 4.0;
        private const double WarmUp = 1.0;

        private struct Pacing
        {
            public float LagSpreadMs;    // max - min lag of the drawn body behind the true one
            public float FrozenPerSecond; // frames that moved under half the even step
            public float DoubledPerSecond; // frames that moved over 1.5 × the even step
        }

        private static Pacing Measure(float refreshHz)
        {
            double tick = 1.0 / TickRate;
            double frame = 1.0 / refreshHz;
            var interpolation = new HumanoidVisualInterpolation();

            int nextTick = 0;
            double previousDrawn = double.NaN;
            double minLag = double.MaxValue, maxLag = double.MinValue;
            int frozen = 0, doubled = 0;
            int frames = (int)System.Math.Ceiling(Seconds * refreshHz);

            for (int k = 0; k < frames; k++)
            {
                double now = k * frame;

                // NGO: every tick whose time has passed fires in this frame (several when frames are slower than ticks).
                // Inside a tick, NetworkManager.LocalTime is that tick's exact time; HumanoidVisualSmoothingView stamps
                // the commit with it, and evaluates at LocalTime between ticks (here: the frame's time).
                while (nextTick * tick <= now + 1e-9)
                {
                    float tickTime = (float)(nextTick * tick);
                    interpolation.Commit(null, new Vector3(tickTime * Speed, 0f, 0f), Quaternion.identity, tickTime);
                    nextTick++;
                }

                interpolation.Evaluate((float)now, (float)frame, out var drawn, out _);

                if (!double.IsNaN(previousDrawn) && now > WarmUp)
                {
                    double lag = (now * Speed - drawn.x) / Speed;
                    minLag = System.Math.Min(minLag, lag);
                    maxLag = System.Math.Max(maxLag, lag);

                    double step = (drawn.x - previousDrawn) / (Speed * frame);
                    if (step < 0.5) frozen++;
                    if (step > 1.5) doubled++;
                }
                previousDrawn = drawn.x;
            }

            float measured = (float)(Seconds - WarmUp);
            return new Pacing
            {
                LagSpreadMs = (float)((maxLag - minLag) * 1000.0),
                FrozenPerSecond = frozen / measured,
                DoubledPerSecond = doubled / measured,
            };
        }

        // Rates that are not a multiple of the 30 Hz tick are the ones that juddered while ticks were stamped with frame
        // time (2026-11-27: lag wobbled 8–16 ms at 75/144/165 Hz with VSync).
        [TestCase(1000f)] // VSync off, uncapped
        [TestCase(240f)]
        [TestCase(165f)]
        [TestCase(144f)]
        [TestCase(120f)]
        [TestCase(75f)]
        [TestCase(60f)]
        public void DrawnBody_MovesEvenly_AtDisplayRate(float refreshHz)
        {
            var pacing = Measure(refreshHz);
            string report = $"{refreshHz} Hz: lag wobbles {pacing.LagSpreadMs:F1} ms, " +
                            $"{pacing.FrozenPerSecond:F1} frozen and {pacing.DoubledPerSecond:F1} doubled frames per second";
            TestContext.WriteLine(report);

            Assert.That(pacing.LagSpreadMs, Is.LessThan(1f), report);
            Assert.That(pacing.FrozenPerSecond + pacing.DoubledPerSecond, Is.Zero, report);
        }
    }
}
