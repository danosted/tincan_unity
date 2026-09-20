#nullable enable
using NUnit.Framework;
using TinCan.Features.CloudBoundary;

namespace TinCan.Tests.EditMode
{
    public class CloudSubmersionProcessorTests
    {
        private CloudSubmersionProcessor _processor = null!;

        [SetUp]
        public void SetUp()
        {
            _processor = new CloudSubmersionProcessor();
        }

        [Test]
        public void CalculateSubmersion_UsesSurfaceHeightMinusAltitudeAndClampsToZeroToOne()
        {
            Assert.That(CloudSubmersionProcessor.CalculateSubmersion(surfaceHeight: 100f, altitude: 80f, transitionDepth: 25f), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(CloudSubmersionProcessor.CalculateSubmersion(surfaceHeight: 100f, altitude: 120f, transitionDepth: 25f), Is.Zero);
            Assert.That(CloudSubmersionProcessor.CalculateSubmersion(surfaceHeight: 100f, altitude: 0f, transitionDepth: 25f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Evaluate_IncreasesWhiteoutAndMufflingAsShipSubmerges()
        {
            CloudSubmersionState shallow = _processor.Evaluate(surfaceHeight: 100f, altitude: 90f, transitionDepth: 25f);
            CloudSubmersionState deep = _processor.Evaluate(surfaceHeight: 100f, altitude: 60f, transitionDepth: 25f);

            Assert.That(shallow.Submersion, Is.LessThan(deep.Submersion));
            Assert.That(shallow.Whiteout, Is.LessThan(deep.Whiteout));
            Assert.That(shallow.LightMultiplier, Is.GreaterThan(deep.LightMultiplier));
            Assert.That(shallow.VapourIntensity, Is.LessThan(deep.VapourIntensity));
            Assert.That(shallow.AudioMuffle, Is.LessThan(deep.AudioMuffle));
        }
    }
}
