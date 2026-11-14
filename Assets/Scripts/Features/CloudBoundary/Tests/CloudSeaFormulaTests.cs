#nullable enable
using System;
using NUnit.Framework;
using UnityEngine;

namespace TinCan.Features.CloudBoundary.Tests
{
    public class CloudSeaFormulaTests
    {
        private static readonly CloudSwell Swell = new(20f, 1000f, 3f, CloudSwell.OffsetOf(7));

        [Test]
        public void Swell_IsZero_WithoutAmplitude()
        {
            Assert.That(CloudSeaFormula.Swell(123f, -45f, 10f, CloudSwell.None), Is.EqualTo(0f));
        }

        [Test]
        public void Swell_StaysWithinTheAmplitude_AndRolls()
        {
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int i = 0; i < 400; i++)
            {
                float height = CloudSeaFormula.Swell(i * 37f, i * -23f, 0f, Swell);
                lowest = Mathf.Min(lowest, height);
                highest = Mathf.Max(highest, height);
            }

            Assert.That(lowest, Is.GreaterThanOrEqualTo(-20f));
            Assert.That(highest, Is.LessThanOrEqualTo(20f));
            Assert.That(highest - lowest, Is.GreaterThan(10f), "the swell has real hills and troughs");
            Assert.That(CloudSeaFormula.Swell(0f, 0f, 30f, Swell), Is.Not.EqualTo(CloudSeaFormula.Swell(0f, 0f, 0f, Swell)).Within(1e-3f),
                "the swell moves with time");
        }

        [Test]
        public void HoleCut_IsFull_InsideAnOpenHole_AndNoneBeyondItsRim()
        {
            var holes = new[] { new CloudHoleShape(new Vector2(100f, 0f), 50f, 20f, 1f) };

            Assert.That(CloudSeaFormula.HoleCut(100f, 0f, holes), Is.EqualTo(1f));
            Assert.That(CloudSeaFormula.HoleCut(149f, 0f, holes), Is.EqualTo(1f).Within(1e-3f));
            Assert.That(CloudSeaFormula.HoleCut(160f, 0f, holes), Is.EqualTo(0.5f).Within(1e-3f), "half way across the rim");
            Assert.That(CloudSeaFormula.HoleCut(171f, 0f, holes), Is.EqualTo(0f));
        }

        [Test]
        public void HoleCut_OfAPartlyOpenHole_IsAtMostItsOpenness()
        {
            var holes = new[] { new CloudHoleShape(Vector2.zero, 10f, 20f, 0.4f) };

            Assert.That(CloudSeaFormula.HoleCut(0f, 0f, holes), Is.EqualTo(0.4f).Within(1e-5f));
        }

        [Test]
        public void HoleCut_TakesTheDeepestOfOverlappingHoles()
        {
            var holes = new[]
            {
                new CloudHoleShape(Vector2.zero, 10f, 20f, 0.3f),
                new CloudHoleShape(new Vector2(5f, 0f), 10f, 20f, 0.9f)
            };

            Assert.That(CloudSeaFormula.HoleCut(5f, 0f, holes), Is.EqualTo(0.9f).Within(1e-5f));
        }

        [Test]
        public void Height_DropsByTheHoleDepth_InsideAHole()
        {
            var holes = new[] { new CloudHoleShape(Vector2.zero, 50f, 10f, 1f) };

            float open = CloudSeaFormula.Height(0f, 0f, 0f, 5f, CloudSwell.None, Array.Empty<CloudHoleShape>(), 400f);
            float hole = CloudSeaFormula.Height(0f, 0f, 0f, 5f, CloudSwell.None, holes, 400f);

            Assert.That(open, Is.EqualTo(5f));
            Assert.That(hole, Is.EqualTo(-395f));
        }
    }
}
