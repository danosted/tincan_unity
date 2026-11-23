#nullable enable
using NUnit.Framework;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.Storm.Tests
{
    /// <summary>The storm's look and sound rules (<see cref="StormProcessor"/>) and its weather (<see cref="StormWeatherPresenter"/>).</summary>
    public class StormTests
    {
        private sealed class Intensity : IStormIntensity
        {
            public float Depth { get; set; }
            public float Strength { get; set; } = 1f;
        }

        private StormConfig _config = null!;
        private Intensity _intensity = null!;
        private StormSoundPresenter _sound = null!;
        private StormWeatherPresenter _weather = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<StormConfig>();
            _config.StrikesPerMinute = 40f;
            _config.LightningMinWeight = 0.25f;
            _intensity = new Intensity();
            _weather = new StormWeatherPresenter(_config, _intensity, new FakeTimeService(), _sound = new StormSoundPresenter(_config));
        }

        [TearDown]
        public void TearDown()
        {
            _sound.Dispose();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void TheWeight_IsDepthTimesStrength()
        {
            Assert.That(StormProcessor.Weight(0f, 1f), Is.Zero, "outside the wall");
            Assert.That(StormProcessor.Weight(1f, 1f), Is.EqualTo(1f), "at the heart, raging");
            Assert.That(StormProcessor.Weight(1f, 0f), Is.Zero, "cured");
            Assert.That(StormProcessor.Weight(0.5f, 0.5f), Is.EqualTo(0.25f));
        }

        [Test]
        public void Lightning_NeedsEnoughWeight_AndComesMoreOftenDeeper()
        {
            Assert.That(StormProcessor.StrikeChance(0.2f, 40f, 0.25f), Is.Zero);
            Assert.That(StormProcessor.StrikeChance(1f, 40f, 0.25f), Is.GreaterThan(StormProcessor.StrikeChance(0.6f, 40f, 0.25f)));
        }

        [Test]
        public void TheStrikes_AreTheSameForTheSameSeedAndSlot()
        {
            for (int slot = 0; slot < 50; slot++)
            {
                Assert.That(StormProcessor.StrikeIn(slot, 7, 0.5f), Is.EqualTo(StormProcessor.StrikeIn(slot, 7, 0.5f)));
            }
        }

        [Test]
        public void AFlash_FlickersAndFades()
        {
            Assert.That(StormProcessor.Flash(-0.1f, 0.4f), Is.Zero);
            Assert.That(StormProcessor.Flash(0.01f, 0.4f), Is.GreaterThan(StormProcessor.Flash(0.08f, 0.4f)), "the first flicker, then a dip");
            Assert.That(StormProcessor.Flash(0.5f, 0.4f), Is.Zero);
        }

        [Test]
        public void TheHeartsPulse_IsHeardOnlyNearTheEye_AndNotOnceCured()
        {
            Assert.That(StormProcessor.HeartShare(0.4f, 1f, 0.6f), Is.Zero);
            Assert.That(StormProcessor.HeartShare(1f, 1f, 0.6f), Is.EqualTo(1f));
            Assert.That(StormProcessor.HeartShare(1f, 0f, 0.6f), Is.Zero);
        }

        [Test]
        public void TheWeather_FollowsTheIntensity_AndFlashesOnlyInADeepStorm()
        {
            _intensity.Depth = 0f;
            float calmFlash = 0f;
            for (float t = 0f; t < 120f; t += 0.05f)
            {
                _weather.Step(t);
                calmFlash = Mathf.Max(calmFlash, _weather.Flash);
            }
            Assert.That(_weather.Weight, Is.Zero);
            Assert.That(calmFlash, Is.Zero, "no lightning in a calm sky");

            _intensity.Depth = 1f;
            float stormFlash = 0f;
            for (float t = 120f; t < 240f; t += 0.05f)
            {
                _weather.Step(t);
                stormFlash = Mathf.Max(stormFlash, _weather.Flash);
            }
            Assert.That(_weather.Weight, Is.EqualTo(1f));
            Assert.That(stormFlash, Is.GreaterThan(0.5f), "lightning at the heart");
        }
    }
}
