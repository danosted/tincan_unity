#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Humanoid;

namespace TinCan.Tests.EditMode
{
    public class InputLeadProcessorTests
    {
        private readonly InputLeadProcessor _lead = new();

        [Test]
        public void WithinTheDeadBand_LeavesTheBufferAlone()
        {
            float inside = InputLeadProcessor.DeadBand * 0.8f;
            Assert.That(_lead.Next(0.033, InputLeadProcessor.TargetDepth + inside, 1f), Is.EqualTo(0.033));
            Assert.That(_lead.Next(0.033, InputLeadProcessor.TargetDepth - inside, 1f), Is.EqualTo(0.033));
        }

        [Test]
        public void TooDeep_LowersSlowly_TooShallow_RaisesFaster()
        {
            Assert.That(_lead.Next(0.033, 3.5f, 0.1f), Is.EqualTo(0.033 - InputLeadProcessor.LowerRate * 0.1).Within(1e-9));
            Assert.That(_lead.Next(0.033, 0.2f, 0.1f), Is.EqualTo(0.033 + InputLeadProcessor.RaiseRate * 0.1).Within(1e-9));
        }

        [Test]
        public void StaysWithinBounds_AndCapsLongGaps()
        {
            Assert.That(_lead.Next(InputLeadProcessor.MinBufferSec, 4f, 0.2f), Is.EqualTo(InputLeadProcessor.MinBufferSec));
            Assert.That(_lead.Next(InputLeadProcessor.MaxBufferSec, 0f, 0.2f), Is.EqualTo(InputLeadProcessor.MaxBufferSec));
            Assert.That(_lead.Next(0.033, 4f, 30f), Is.EqualTo(0.033 - InputLeadProcessor.LowerRate * InputLeadProcessor.MaxStepSec).Within(1e-9));
            Assert.That(_lead.Next(0.033, 4f, 0f), Is.EqualTo(0.033));
        }

        /// <summary>
        /// Closed loop with the lags measured on 2026-10-03: the server's reported depth follows the lead with a delay
        /// (a 1 s average plus the snapshot round trip), and each tick of extra lead (33 ms) is one more queued input.
        /// Starting from the measured state (depth ~3.7), the depth must settle in the target band and stay there.
        /// </summary>
        [Test]
        public void ClosedLoop_SettlesInTheBandWithoutHunting()
        {
            const float tick = 1f / 30f;
            const int lagTicks = 45; // ~1.5 s from a lead change to the reported depth
            double buffer = tick;
            double baselineLead = 0.110 + tick; // inflated half-RTT estimate + one tick
            var history = new Queue<double>();
            float reported = 3.7f;
            var settled = new List<float>();

            for (int step = 0; step < 30 * 120; step++)
            {
                double lead = baselineLead + (buffer - tick);
                history.Enqueue(lead);
                double laggedLead = history.Count > lagTicks ? history.Dequeue() : baselineLead;
                // Measured: a lead of ~143 ms (4.3 ticks) stood at a depth of ~3.7.
                float actualDepth = (float)System.Math.Max(0.0, laggedLead / tick - 0.6);
                reported += (actualDepth - reported) * HumanoidInputBuffer.DepthSmoothing;

                buffer = _lead.Next(buffer, reported, tick);
                if (step > 30 * 60) settled.Add(reported);
            }

            float min = float.MaxValue, max = float.MinValue;
            foreach (var depth in settled) { min = System.Math.Min(min, depth); max = System.Math.Max(max, depth); }
            Assert.That(min, Is.GreaterThanOrEqualTo(InputLeadProcessor.TargetDepth - InputLeadProcessor.DeadBand - 0.1f), $"depth after a minute: {min}..{max}");
            Assert.That(max, Is.LessThanOrEqualTo(InputLeadProcessor.TargetDepth + InputLeadProcessor.DeadBand + 0.1f), $"depth after a minute: {min}..{max}");
        }
    }
}
