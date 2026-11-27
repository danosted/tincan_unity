#nullable enable
using NUnit.Framework;
using UnityEngine;

namespace TinCan.DevTools.Tests
{
    public class FramePacingMeterTests
    {
        private const float Frame = 1f / 144f;
        private const float Speed = 20f;

        [Test]
        public void SteadyMotion_HasNoUnevenFrames()
        {
            var meter = new FramePacingMeter();
            for (int i = 0; i < 144 * 3; i++) meter.Sample(new Vector3(i * Frame * Speed, 0f, 0f), Frame);

            Assert.That(meter.MovingSeconds, Is.GreaterThan(2f));
            Assert.That(meter.UnevenFrames, Is.Zero);
        }

        [Test]
        public void TickSteppedMotion_IsUnevenMostFrames()
        {
            var meter = new FramePacingMeter();
            for (int i = 0; i < 144 * 3; i++)
            {
                float time = i * Frame;
                float tickTime = Mathf.Floor(time * 30f) / 30f; // drawn where the last 30 Hz tick left it
                meter.Sample(new Vector3(tickTime * Speed, 0f, 0f), Frame);
            }

            Assert.That(meter.UnevenPerSecond, Is.GreaterThan(60f));
        }

        [Test]
        public void SmoothAcceleration_HasNoUnevenFrames()
        {
            var meter = new FramePacingMeter();
            for (int i = 0; i < 144 * 3; i++)
            {
                float time = i * Frame;
                meter.Sample(new Vector3(0.5f * 10f * time * time, 0f, 0f), Frame); // 10 m/s², from rest
            }

            Assert.That(meter.MovingSeconds, Is.GreaterThan(2f));
            Assert.That(meter.UnevenFrames, Is.Zero);
        }

        [Test]
        public void StandingStill_IsNotMeasured()
        {
            var meter = new FramePacingMeter();
            for (int i = 0; i < 144; i++) meter.Sample(Vector3.zero, Frame);

            Assert.That(meter.MovingSeconds, Is.Zero);
            Assert.That(meter.Describe(), Is.EqualTo("not moving"));
        }
    }
}
