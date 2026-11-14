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

        private static DayCycleUseCase Day(DayCycleConfig config, FakeTimeService time, FakeSessionProgress? session = null) =>
            new(config, time, new SunPathProcessor(), session);

        [Test]
        public void TheVoyageDriver_ReadsTheSessionsProgress()
        {
            _config.Driver = new VoyageDayDriver { CastOffHour = 7f, ArrivalHour = 17f };
            var session = new FakeSessionProgress();
            var day = Day(_config, new FakeTimeService { Time = 50f }, session);
            Assert.That(day.Hour, Is.EqualTo(7f), "no voyage under way: cast-off");

            session.Progress = 0.5f;

            Assert.That(day.Hour, Is.EqualTo(12f));
            Assert.That(day.Elevation, Is.EqualTo(12f).Within(0.01f));
        }

        [Test]
        public void TheSun_FollowsTheSharedClock()
        {
            var time = new FakeTimeService { Time = 0f };
            var day = Day(_config, time);
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
            var host = Day(_config, time);
            var client = Day(_config, time);

            Assert.That(client.ToSun, Is.EqualTo(host.ToSun));
        }

        [Test]
        public void TheDriver_SwapsWhileRunning()
        {
            var day = Day(_config, new FakeTimeService { Time = 10f });

            day.Clock.Driver = new FixedHourDayDriver { Hour = 12f };

            Assert.That(day.Hour, Is.EqualTo(12f));
        }
    }
}
