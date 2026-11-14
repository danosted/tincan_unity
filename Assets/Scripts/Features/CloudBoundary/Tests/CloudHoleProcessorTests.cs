#nullable enable
using NUnit.Framework;
using UnityEngine;

namespace TinCan.Features.CloudBoundary.Tests
{
    public class CloudHoleProcessorTests
    {
        private readonly CloudHoleProcessor _processor = new();

        private static CloudHole Hole() =>
            new(new Vector2(10f, 20f), 100f, openedAt: 50f, openSeconds: 10f, closesAt: 100f, closeSeconds: 20f, drift: new Vector2(2f, 0f));

        [Test]
        public void Openness_EasesOpen_StaysOpen_ThenEasesShut()
        {
            CloudHole hole = Hole();

            Assert.That(_processor.Openness(hole, 40f), Is.EqualTo(0f));
            Assert.That(_processor.Openness(hole, 55f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(_processor.Openness(hole, 80f), Is.EqualTo(1f));
            Assert.That(_processor.Openness(hole, 110f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(_processor.Openness(hole, 125f), Is.EqualTo(0f));
        }

        [Test]
        public void Openness_OfAHoleThatNeverCloses_StaysOpen()
        {
            var hole = new CloudHole(Vector2.zero, 50f, openedAt: 0f, openSeconds: 5f);

            Assert.That(_processor.Openness(hole, 1e6f), Is.EqualTo(1f));
            Assert.That(_processor.HasClosed(hole, 1e6f), Is.False);
        }

        [Test]
        public void CentreAt_DriftsFromWhereItOpened()
        {
            CloudHole hole = Hole();

            Assert.That(_processor.CentreAt(hole, 0f), Is.EqualTo(new Vector2(10f, 20f)), "no drift before it opens");
            Assert.That(_processor.CentreAt(hole, 60f), Is.EqualTo(new Vector2(30f, 20f)));
        }

        [Test]
        public void HasClosed_OnlyOnceFullyShut()
        {
            CloudHole hole = Hole();

            Assert.That(_processor.HasClosed(hole, 119f), Is.False);
            Assert.That(_processor.HasClosed(hole, 120f), Is.True);
        }

        [Test]
        public void Shape_GrowsTheOpenPartWithTheOpenness_AndKeepsTheRim()
        {
            CloudHoleShape half = _processor.Shape(Hole(), 55f, 0.35f);

            Assert.That(half.Radius, Is.EqualTo(50f).Within(0.01f));
            Assert.That(half.Rim, Is.EqualTo(35f).Within(1e-4f));
            Assert.That(half.Openness, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(half.Centre, Is.EqualTo(new Vector2(20f, 20f)));
        }
    }
}
