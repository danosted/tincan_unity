#nullable enable
using NUnit.Framework;
using UnityEngine;

namespace TinCan.Features.CloudBoundary.Tests
{
    /// <summary><see cref="CloudWispsProcessor"/>. Plan: .docs/plans/cloud-sea.md, step 3.</summary>
    public class CloudWispsProcessorTests
    {
        private readonly CloudWispsProcessor _wisps = new();
        private CloudWispsConfig _config = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CloudWispsConfig>();
            _config.SpeedStart = 4f;
            _config.SpeedFull = 40f;
            _config.AboveSea = 0.3f;
            _config.UnderFloor = 0.5f;
            _config.EngulfStart = 0.85f;
            _config.MaxRate = 100f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void HoldingStill_AboveTheSea_MakesNoWisps()
        {
            Assert.That(_wisps.Evaluate(2f, 0f, _config).Rate, Is.EqualTo(0f));
        }

        [Test]
        public void Moving_AboveTheSea_MakesALightStream_ThatGrowsWithSpeed()
        {
            float slow = _wisps.Evaluate(15f, 0f, _config).Rate;
            float fast = _wisps.Evaluate(40f, 0f, _config).Rate;

            Assert.That(slow, Is.GreaterThan(0f));
            Assert.That(fast, Is.GreaterThan(slow));
            Assert.That(fast, Is.EqualTo(30f).Within(1e-3f), "a light stream: AboveSea of the most");
        }

        [Test]
        public void InTheCloud_ThereAreWisps_EvenHoldingStill_AndMoreThanAboveItAtSpeed()
        {
            Assert.That(_wisps.Evaluate(0f, 0.2f, _config).Rate, Is.GreaterThan(0f));
            Assert.That(_wisps.Evaluate(40f, 0.2f, _config).Rate, Is.GreaterThan(_wisps.Evaluate(40f, 0f, _config).Rate));
        }

        [Test]
        public void DeeperInTheCloud_MakesMoreWisps()
        {
            Assert.That(_wisps.Evaluate(10f, 0.8f, _config).Rate, Is.GreaterThan(_wisps.Evaluate(10f, 0.2f, _config).Rate));
        }

        [Test]
        public void NearFullSubmersion_TheWispsFadeIntoTheFog()
        {
            Assert.That(_wisps.Evaluate(10f, 0.5f, _config).Opacity, Is.EqualTo(1f));
            Assert.That(_wisps.Evaluate(10f, 1f, _config).Opacity, Is.LessThan(0.5f));
        }

        [Test]
        public void EnteringTheCloud_IsTheMomentForABurst_OnlyOnce()
        {
            Assert.That(_wisps.Entered(0f, 0.05f), Is.True);
            Assert.That(_wisps.Entered(0.05f, 0.1f), Is.False);
            Assert.That(_wisps.Entered(0f, 0f), Is.False);
        }
    }
}
