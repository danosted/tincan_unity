#nullable enable
using NUnit.Framework;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.CloudFlight.Tests
{
    /// <summary><see cref="CloudFlightProcessor"/>. Plan: .docs/plans/cloud-sea.md, "Soaring and cloud shelves".</summary>
    public class CloudFlightProcessorTests
    {
        private const float Step = 0.02f;
        private const float InCloud = -10f;
        private const float InAir = 100f;

        private readonly CloudFlightProcessor _flight = new();
        private CloudFlightConfig _config = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CloudFlightConfig>();
            _config.LiftLean = 0f; // the lean with the rise and fall is tested on its own, below
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private AirshipFlightForce At(float slope, float height, float engine = 15f, float momentum = 0f, float fall = 0f) =>
            _flight.Evaluate(slope, height, engine, momentum, fall, Step, _config);

        // Holds the ship where it is for some seconds, carrying the fall from step to step.
        private CloudFlightReading Held(float seconds, float slope, float height, float engine = 15f, float momentum = 0f, float fall = 0f)
        {
            var reading = _flight.Read(slope, height, engine, momentum, fall, Step, _config);
            for (float t = Step; t < seconds; t += Step)
                reading = _flight.Read(slope, height, engine, momentum, reading.Fall, Step, _config);
            return reading;
        }

        [Test]
        public void Diving_BuildsSpeed_FasterInTheCloudThanInOpenAir()
        {
            float cloud = At(-0.5f, InCloud).MomentumChange;
            float air = At(-0.5f, InAir).MomentumChange;

            Assert.That(air, Is.GreaterThan(0f));
            Assert.That(cloud, Is.GreaterThan(air * 2f), "the cloud carries you");
        }

        [Test]
        public void Climbing_SpendsSpeed_ButLittleOfItInTheCloud()
        {
            float cloud = At(0.5f, InCloud, momentum: 20f).MomentumChange;
            float air = At(0.5f, InAir, momentum: 20f).MomentumChange;

            Assert.That(air, Is.LessThan(0f));
            Assert.That(cloud, Is.GreaterThan(air * 0.5f), "climbing in cloud costs less");
        }

        [Test]
        public void AGentleClimb_IsHeldByTheEngine_ASteeperOneSpendsSpeed()
        {
            float gentle = Mathf.Sin((_config.FreeClimb - 1f) * Mathf.Deg2Rad);
            float steep = Mathf.Sin((_config.FreeClimb + 10f) * Mathf.Deg2Rad);

            Assert.That(At(gentle, InAir).MomentumChange, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(At(steep, InAir).MomentumChange, Is.LessThan(0f));
        }

        [Test]
        public void OnTheSea_LevelIsItsSlope_AndRidingItNeitherBuildsNorSpendsSpeed()
        {
            float downhill = -6f;
            float nose = Mathf.Sin(downhill * Mathf.Deg2Rad);

            var riding = _flight.Evaluate(nose, InCloud, 15f, 0f, 0f, Step, _config, seaTilt: downhill);

            Assert.That(riding.Level, Is.EqualTo(6f).Within(1e-4f), "level is the slope, nose down");
            Assert.That(riding.MomentumChange, Is.EqualTo(0f).Within(1e-5f), "riding the swell is neutral");
        }

        [Test]
        public void OnTheSea_ANoseLaggingTheSwellALittle_DoesNotCreepAStillShipOn()
        {
            // Engine off, nose level, the sea rising 2 degrees ahead: the nose has not caught up yet. No speed from that.
            Assert.That(_flight.Evaluate(0f, InCloud, 0f, 0f, 0f, Step, _config, seaTilt: 2f).MomentumChange,
                Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void OnTheSea_ANosePushedBelowItsSlope_StillCharges()
        {
            float nose = Mathf.Sin(-20f * Mathf.Deg2Rad);

            Assert.That(_flight.Evaluate(nose, InCloud, 15f, 0f, 0f, Step, _config, seaTilt: -6f).MomentumChange,
                Is.GreaterThan(0f));
        }

        [Test]
        public void TheSeaSlopeFollowed_IsCapped_AndInOpenAirLevelIsTheHorizon()
        {
            var rim = _flight.Evaluate(0f, InCloud, 15f, 0f, 0f, Step, _config, seaTilt: -40f);
            var air = _flight.Evaluate(0f, InAir, 15f, 0f, 0f, Step, _config, seaTilt: -6f);

            Assert.That(rim.Level, Is.EqualTo(_config.MaxContourTilt).Within(1e-4f), "a hole's rim is a lip, not a slide");
            Assert.That(air.Level, Is.EqualTo(0f));
        }

        [Test]
        public void TheNoseLeans_UpAsTheCloudLiftsTheShip_DownAsItFalls_Capped()
        {
            _config.LiftLean = 1f;
            _config.MaxLiftLean = 6f;

            var lifted = _flight.Evaluate(0f, -2f, 15f, 0f, 0f, Step, _config);
            var falling = _flight.Evaluate(0f, InAir, 15f, 0f, fall: 4f, Step, _config);
            var deep = _flight.Evaluate(0f, -100f, 15f, 0f, 0f, Step, _config);

            Assert.That(lifted.Level, Is.EqualTo(-2f).Within(1e-3f), "lifted 2 m/s: the nose 2 degrees up");
            Assert.That(falling.Level, Is.EqualTo(4f).Within(1e-3f), "falling 4 m/s: the nose 4 degrees down");
            Assert.That(deep.Level, Is.EqualTo(-6f).Within(1e-3f), "never more than the most");
        }

        [Test]
        public void ANoseLeaningWithTheRiseAndFall_NeitherBuildsNorSpendsSpeed()
        {
            _config.LiftLean = 1f;
            float down = Mathf.Sin(-4f * Mathf.Deg2Rad);
            float up = Mathf.Sin(2f * Mathf.Deg2Rad);

            Assert.That(_flight.Evaluate(down, InAir, 15f, 0f, fall: 4f, Step, _config).MomentumChange,
                Is.EqualTo(0f).Within(1e-5f), "nose down with the fall is no dive");
            Assert.That(_flight.Evaluate(up, -2f, 15f, 0f, 0f, Step, _config).MomentumChange,
                Is.EqualTo(0f).Within(1e-5f), "nose up with the lift is no climb");
        }

        [Test]
        public void TheReading_ShowsTheSpeedAndWhetherItIsCharging()
        {
            var diving = _flight.Read(-0.5f, InCloud, 15f, 5f, 0f, Step, _config);
            var climbing = _flight.Read(0.6f, InAir, 15f, 5f, 0f, Step, _config);

            Assert.That(diving.Charge, Is.GreaterThan(0f));
            Assert.That(climbing.Charge, Is.LessThan(0f));
            Assert.That(diving.Speed, Is.EqualTo(15f + 5f + diving.Force.MomentumChange).Within(1e-4f));
            Assert.That(diving.InCloud, Is.EqualTo(1f));
        }

        [Test]
        public void TheSpeed_StaysBetweenStillAndTheMost()
        {
            Assert.That(15f + 50f + At(-1f, InCloud, momentum: 50f).MomentumChange, Is.LessThanOrEqualTo(_config.MaxSpeed + 1e-3f));
            Assert.That(15f - 15f + At(1f, InAir, momentum: -15f).MomentumChange, Is.GreaterThanOrEqualTo(-1e-3f), "never flies backwards");
        }

        [Test]
        public void OffTheSea_ThePullBuilds_ToASlowFallAtSpeed_AndAFastOneWhenStalled()
        {
            float first = At(0f, InAir, momentum: 20f).Vertical;
            float gliding = Held(10f, 0f, InAir, momentum: 20f).Force.Vertical;
            float stalled = Held(10f, 0f, InAir, engine: 0f).Force.Vertical;

            Assert.That(first, Is.LessThan(0f).And.GreaterThan(-0.5f), "the pull starts gently");
            Assert.That(gliding, Is.EqualTo(-_config.GlideFall).Within(1e-3f));
            Assert.That(stalled, Is.EqualTo(-_config.StallFall).Within(1e-3f));
        }

        [Test]
        public void OnTheSea_TheCloudCarriesTheShipToItsRestHeight_MovingOrNot()
        {
            Assert.That(At(0f, InCloud).Vertical, Is.EqualTo(-_config.Buoyancy * InCloud).Within(1e-3f), "pushed up, moving");
            Assert.That(At(0f, InCloud, engine: 0f).Vertical, Is.EqualTo(-_config.Buoyancy * InCloud).Within(1e-3f), "and still");
            Assert.That(At(0f, -100f).Vertical, Is.EqualTo(_config.MaxHeave).Within(1e-3f), "deep in, the most");
        }

        [Test]
        public void TheShipRidesTheSwell_LiftedAsTheSeaRisesUnderIt_SettledAsItFalls()
        {
            // At rest on the tops: the sea rising under the ship puts it below its rest height, falling puts it above.
            Assert.That(At(0f, -1f).Vertical, Is.GreaterThan(0f));
            Assert.That(At(0f, 1f).Vertical, Is.LessThan(0f));
        }

        [Test]
        public void BackOnTheSea_TheCloudTakesTheFallAway()
        {
            var falling = Held(10f, 0f, InAir, momentum: 20f);
            var caught = Held(2f, 0f, InCloud, momentum: 20f, fall: falling.Fall);

            Assert.That(caught.Fall, Is.EqualTo(0f));
            Assert.That(caught.Force.Vertical, Is.GreaterThan(0f));
        }

        [Test]
        public void AStalledShip_DropsItsRaisedNose_ButNotBelowLevel()
        {
            Assert.That(At(0.4f, InAir, engine: 0f).NoseDrop, Is.EqualTo(_config.NoseDrop).Within(1e-3f));
            Assert.That(At(0f, InAir, engine: 0f).NoseDrop, Is.EqualTo(0f));
            Assert.That(At(0.4f, InAir, momentum: 20f).NoseDrop, Is.EqualTo(0f).Within(1e-3f), "not at speed");
        }

        [Test]
        public void HighUp_TheAirThins_AndTheShipSinksFaster()
        {
            float high = _config.CeilingStart + _config.CeilingDepth;

            Assert.That(Held(20f, 0f, high, momentum: 20f).Force.Vertical, Is.EqualTo(-_config.GlideFall - _config.CeilingSink).Within(1e-3f));
        }

        [Test]
        public void DiveThenPullUp_LaunchesTheShipHighIntoTheSky_AndItGlidesHome()
        {
            // A ship flown by the forces alone, its nose set by hand: 4 s diving through the cloud, then nose up.
            float height = InCloud, momentum = 0f, fall = 0f, slope = -0.5f, highest = height;
            for (float t = 0f; t < 60f; t += Step)
            {
                if (t >= 4f) slope = height > 0f ? 0.6f : 0.3f;
                if (t >= 4f && momentum <= 0f) slope = 0f; // out of speed, level off and glide
                var reading = _flight.Read(slope, height, 15f, momentum, fall, Step, _config);
                momentum += reading.Force.MomentumChange;
                fall = reading.Fall;
                height += ((15f + momentum) * slope + reading.Force.Vertical) * Step;
                highest = Mathf.Max(highest, height);
            }

            Assert.That(highest, Is.GreaterThan(80f), "the launch carries far above the cloud");
            Assert.That(height, Is.LessThan(highest), "and the glide comes back down");
        }
    }
}
