#nullable enable
using NUnit.Framework;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Core.Ship.Tests
{
    public class ShipDrawnPoseTests
    {
        private const int TickRate = 30;
        private const float Tick = 1f / TickRate;

        [Test]
        public void BetweenTicks_BlendsFromThePoseBeforeTheTick()
        {
            var pose = new ShipDrawnPose();
            pose.BeginTick(Vector3.zero, Quaternion.identity, 1f);
            var simulated = new Vector3(1f, 0f, 0f);

            pose.Evaluate(simulated, Quaternion.identity, 1f, Tick, out var atTick, out _);
            pose.Evaluate(simulated, Quaternion.identity, 1f + Tick * 0.5f, Tick, out var half, out _);
            pose.Evaluate(simulated, Quaternion.identity, 1f + Tick * 3f, Tick, out var late, out _);

            Assert.That(atTick.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(half.x, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(late.x, Is.EqualTo(1f).Within(1e-4f), "holds the simulated pose, no overshoot");
        }

        [Test]
        public void Snap_DrawsTheNewPoseAtOnce()
        {
            var pose = new ShipDrawnPose();
            pose.BeginTick(Vector3.zero, Quaternion.identity, 1f);
            var teleported = new Vector3(500f, 0f, 0f);
            pose.Snap(teleported, Quaternion.identity);

            pose.Evaluate(teleported, Quaternion.identity, 1f, Tick, out var drawn, out _);

            Assert.That(drawn.x, Is.EqualTo(500f).Within(1e-3f));
        }

        [Test]
        public void NoRecentTick_IsInactive()
        {
            var pose = new ShipDrawnPose();
            Assert.That(pose.IsActive(0f), Is.False);

            pose.BeginTick(Vector3.zero, Quaternion.identity, 1f);
            Assert.That(pose.IsActive(1.1f), Is.True);
            Assert.That(pose.IsActive(2f), Is.False);
        }

        /// <summary>
        /// A ship flying straight at 20 m/s, ticks and frames paced as NGO and VSync pace them: the drawn ship must trail
        /// the true one by a steady lag at every refresh rate, including those that are not a multiple of the tick rate.
        /// </summary>
        [TestCase(1000f)]
        [TestCase(240f)]
        [TestCase(165f)]
        [TestCase(144f)]
        [TestCase(120f)]
        [TestCase(75f)]
        [TestCase(60f)]
        public void DrawnShip_MovesEvenly_AtDisplayRate(float refreshHz)
        {
            const float speed = 20f;
            double frame = 1.0 / refreshHz;
            var pose = new ShipDrawnPose();
            int nextTick = 0;
            float simulatedX = 0f;
            double minLag = double.MaxValue, maxLag = double.MinValue;

            for (int k = 0; k < (int)(4.0 * refreshHz); k++)
            {
                double now = k * frame;
                while (nextTick * (double)Tick <= now + 1e-9)
                {
                    float tickTime = nextTick * Tick;
                    pose.BeginTick(new Vector3(simulatedX, 0f, 0f), Quaternion.identity, tickTime);
                    simulatedX = (nextTick + 1) * Tick * speed; // the tick moves the ship one step on
                    nextTick++;
                }

                pose.Evaluate(new Vector3(simulatedX, 0f, 0f), Quaternion.identity, (float)now, Tick, out var drawn, out _);
                if (now < 1.0) continue;

                double lag = now - drawn.x / speed;
                minLag = System.Math.Min(minLag, lag);
                maxLag = System.Math.Max(maxLag, lag);
            }

            float spreadMs = (float)((maxLag - minLag) * 1000.0);
            Assert.That(spreadMs, Is.LessThan(1f), $"{refreshHz} Hz: the drawn ship's lag wobbles {spreadMs:F1} ms");
        }
    }
}
