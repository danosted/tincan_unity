#nullable enable
using NUnit.Framework;
using TinCan.Features.Airship.Fuel;

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
    }
}
