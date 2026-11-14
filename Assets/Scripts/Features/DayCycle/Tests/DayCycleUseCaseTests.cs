#nullable enable
using NUnit.Framework;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.DayCycle.Tests
{
    public class DayCycleUseCaseTests
    {
        private DayCycleConfig _config = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<DayCycleConfig>();
            _config.Driver = new FixedCycleDayDriver { CycleSeconds = 240f, StartHour = 0f };
            _config.Path = SunPath.Default;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void TheSun_FollowsTheSharedClock()
        {
            var time = new FakeTimeService { Time = 0f };
            var day = new DayCycleUseCase(_config, time, new SunPathProcessor());
            Assert.That(day.Hour, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(day.Elevation, Is.EqualTo(-42f).Within(0.01f));

            time.Time = 120f; // half the cycle: noon

            Assert.That(day.Hour, Is.EqualTo(12f).Within(1e-3f));
            Assert.That(day.Elevation, Is.EqualTo(12f).Within(0.01f));
            Assert.That(day.ToSun.z, Is.LessThan(0f));
        }

        [Test]
        public void TwoPeers_OnTheSameClock_SeeTheSameSun()
        {
            var time = new FakeTimeService { Time = 77f };
            var host = new DayCycleUseCase(_config, time, new SunPathProcessor());
            var client = new DayCycleUseCase(_config, time, new SunPathProcessor());

            Assert.That(client.ToSun, Is.EqualTo(host.ToSun));
        }

        [Test]
        public void TheDriver_SwapsWhileRunning()
        {
            var day = new DayCycleUseCase(_config, new FakeTimeService { Time = 10f }, new SunPathProcessor());

            day.Clock.Driver = new FixedHourDayDriver { Hour = 12f };

            Assert.That(day.Hour, Is.EqualTo(12f));
        }
    }
}
