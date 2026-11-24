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
            public (Vector2 Centre, float Radius)? Region { get; set; }
            public Vector3? Camera { get; set; }
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
        public void TheWeight_RisesIntoTheWall_HoldsInTheBands_ClearsInTheEye_TimesStrength()
        {
            Assert.That(StormProcessor.Weight(0f, 1f), Is.Zero, "the wall's foot");
            Assert.That(StormProcessor.Weight(0.4f, 1f), Is.EqualTo(1f), "in the bands, raging");
            Assert.That(StormProcessor.Weight(1f, 1f), Is.Zero, "the eye is calm");
            Assert.That(StormProcessor.Weight(0.4f, 0f), Is.Zero, "cured");
            Assert.That(StormProcessor.Weight(0.4f, 0.5f), Is.EqualTo(0.5f));
            Assert.That(StormProcessor.Profile(0.075f), Is.EqualTo(0.5f).Within(1e-4f), "halfway up the wall");
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

            _intensity.Depth = 0.4f;
            float stormFlash = 0f;
            for (float t = 120f; t < 240f; t += 0.05f)
            {
                _weather.Step(t);
                stormFlash = Mathf.Max(stormFlash, _weather.Flash);
            }
            Assert.That(_weather.Weight, Is.EqualTo(1f));
            Assert.That(stormFlash, Is.GreaterThan(0.5f), "lightning at the heart");
        }

        [Test]
        public void AStormInTheWorld_StrikesSeenFromAfar_WithoutFlashingTheAirThere()
        {
            _intensity.Region = (Vector2.zero, 750f);
            _intensity.Camera = new Vector3(0f, 10f, 3000f);
            _intensity.Depth = 0f;
            float airFlash = 0f;
            for (float t = 0f; t < 120f; t += 0.05f)
            {
                _weather.Step(t);
                airFlash = Mathf.Max(airFlash, _weather.Flash);
            }
            Assert.That(airFlash, Is.Zero, "the air outside stays still; the strikes light the storm's cloud (the shaders)");
        }

        [Test]
        public void AStrike_IsInTheStormsBands_LowInItsColumn_TheSameOnEveryPeer()
        {
            var centre = new Vector2(100f, 1500f);
            for (int slot = 0; slot < 200; slot++)
            {
                Vector3 spot = StormProcessor.StrikeSpot(centre, 750f, 7, slot);
                float depth = 1f - Vector2.Distance(new Vector2(spot.x, spot.z), centre) / 750f;
                Assert.That(depth, Is.InRange(0.2f - 1e-3f, 0.75f + 1e-3f), "in the bands, not the eye or the wall's foot");
                Assert.That(spot.y, Is.InRange(150f, 900f));
                Assert.That(spot, Is.EqualTo(StormProcessor.StrikeSpot(centre, 750f, 7, slot)));
            }
        }

        [Test]
        public void Thunder_ComesLater_Fainter_AndMoreMuffled_TheFartherTheStrike()
        {
            Assert.That(2000f / StormProcessor.SpeedOfSound, Is.EqualTo(5.83f).Within(0.01f), "2 km: about 6 s");
            Assert.That(StormProcessor.ThunderShare(100f), Is.EqualTo(1f), "near: full");
            Assert.That(StormProcessor.ThunderShare(4000f), Is.LessThan(StormProcessor.ThunderShare(1000f)));
            Assert.That(StormProcessor.ThunderCutoff(3000f), Is.LessThan(StormProcessor.ThunderCutoff(200f)));
        }
    }
}
