#nullable enable
using System;
using NUnit.Framework;
using UnityEngine;

namespace TinCan.Features.CloudBoundary.Tests
{
    public class CloudSeaFormulaTests
    {
        private static readonly CloudSwell Swell = new(20f, 1000f, 3f, CloudSwell.OffsetOf(7));

        private static readonly CloudWeb Web = new(500f, 60f, 30f, 0.6f, 0.6f, 1f, CloudSwell.OffsetOf(7));

        [Test]
        public void WebCut_IsZero_WhenTheWebIsOff()
        {
            var off = new CloudWeb(500f, 60f, 30f, 0.6f, 0f, 1f, Vector2.zero);
            for (int i = 0; i < 200; i++) Assert.That(CloudSeaFormula.WebCut(i * 31f, i * -17f, 100f, off), Is.EqualTo(0f));
        }

        [Test]
        public void WebCut_CutsRiftsBetweenCloudIslands()
        {
            int inRift = 0, onCloud = 0;
            for (int i = 0; i < 4000; i++)
            {
                float cut = CloudSeaFormula.WebCut((i % 64) * 47f, (i / 64) * 47f, 300f, Web);
                Assert.That(cut, Is.InRange(0f, 1f));
                if (cut > 0.99f) inRift++;
                if (cut < 0.01f) onCloud++;
            }

            // Rifts are narrow and most of the sea is cloud.
            Assert.That(inRift, Is.GreaterThan(0));
            Assert.That(onCloud, Is.GreaterThan(inRift * 3));
        }

        [Test]
        public void WebCut_IsTheSame_ForTheSameTime_AndChangesSlowly()
        {
            float now = CloudSeaFormula.WebCut(812f, -431f, 1000f, Web);
            Assert.That(CloudSeaFormula.WebCut(812f, -431f, 1000f, Web), Is.EqualTo(now));

            // A second later the web has barely moved: at one cycle an hour a rift's edge creeps, it does not jump.
            int changed = 0;
            for (int i = 0; i < 2000; i++)
            {
                float x = (i % 50) * 41f, z = (i / 50) * 41f;
                if (Mathf.Abs(CloudSeaFormula.WebCut(x, z, 1001f, Web) - CloudSeaFormula.WebCut(x, z, 1000f, Web)) > 0.1f) changed++;
            }

            Assert.That(changed, Is.LessThan(20));
        }

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
        public void HoleCut_OfARift_IsFullAlongItsLine_AndEndsRoundBeyondIt()
        {
            // Along x from -200 to 200, 20 m wide each side.
            var holes = new[] { new CloudHoleShape(Vector2.zero, 20f, 10f, 1f, new Vector2(200f, 0f)) };

            Assert.That(CloudSeaFormula.HoleCut(-180f, 0f, holes), Is.EqualTo(1f), "along its line");
            Assert.That(CloudSeaFormula.HoleCut(150f, 19f, holes), Is.EqualTo(1f).Within(1e-3f), "inside its width");
            Assert.That(CloudSeaFormula.HoleCut(150f, 25f, holes), Is.EqualTo(0.5f).Within(1e-3f), "half way across the rim");
            Assert.That(CloudSeaFormula.HoleCut(0f, 31f, holes), Is.EqualTo(0f), "beside it");
            Assert.That(CloudSeaFormula.HoleCut(225f, 0f, holes), Is.EqualTo(0.5f).Within(1e-3f), "its end is round");
        }

        [Test]
        public void ARaggedRift_IsWiderAndNarrowerAlongItsLength_AndAStraightOneIsNot()
        {
            var straight = new CloudHoleShape(Vector2.zero, 30f, 10f, 1f, new Vector2(500f, 0f));
            var ragged = new CloudHoleShape(Vector2.zero, 30f, 10f, 1f, new Vector2(500f, 0f), 0.8f);
            float least = float.MaxValue, most = 0f;
            for (int i = -10; i <= 10; i++)
            {
                Vector2 at = new(i * 50f, 0f);
                Assert.That(CloudSeaFormula.EdgeRadius(at, straight, out _), Is.EqualTo(30f));
                float radius = CloudSeaFormula.EdgeRadius(at, ragged, out _);
                least = Mathf.Min(least, radius);
                most = Mathf.Max(most, radius);
            }

            Assert.That(least, Is.LessThan(25f));
            Assert.That(most, Is.GreaterThan(35f));
            Assert.That(least, Is.GreaterThanOrEqualTo(30f * 0.25f), "never pinched shut");
            Assert.That(most, Is.LessThanOrEqualTo(30f * 1.8f), "no wider than its raggedness allows");
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
