#nullable enable
using NUnit.Framework;
using TinCan.Features.Airship.Fuel;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class FuelGaugeViewTests
    {
        [Test]
        public void NeedleAngle_MapsLevelBetweenEmptyAndFull()
        {
            Assert.That(FuelGaugeView.NeedleAngle(0f, 100f, 120f, -120f), Is.EqualTo(120f).Within(0.001f));
            Assert.That(FuelGaugeView.NeedleAngle(50f, 100f, 120f, -120f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(FuelGaugeView.NeedleAngle(100f, 100f, 120f, -120f), Is.EqualTo(-120f).Within(0.001f));
        }

        [Test]
        public void NeedleAngle_ClampsOutOfRangeAndHandlesZeroCapacity()
        {
            Assert.That(FuelGaugeView.NeedleAngle(150f, 100f, 120f, -120f), Is.EqualTo(-120f).Within(0.001f));
            Assert.That(FuelGaugeView.NeedleAngle(-5f, 100f, 120f, -120f), Is.EqualTo(120f).Within(0.001f));
            Assert.That(FuelGaugeView.NeedleAngle(50f, 0f, 120f, -120f), Is.EqualTo(120f).Within(0.001f));
        }

        [Test]
        public void NeedleRotation_SwingsAroundTheGivenAxis()
        {
            var tip = FuelGaugeView.NeedleRotation(Quaternion.identity, Vector3.up, 90f) * Vector3.forward;

            Assert.That(tip.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(tip.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(tip.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void NeedleRotation_KeepsTheNeutralPoseAtZeroAngle()
        {
            var neutral = Quaternion.Euler(10f, 20f, 30f);

            var rotation = FuelGaugeView.NeedleRotation(neutral, Vector3.up, 0f);

            Assert.That(Quaternion.Angle(rotation, neutral), Is.LessThan(0.001f));
        }
    }
}
