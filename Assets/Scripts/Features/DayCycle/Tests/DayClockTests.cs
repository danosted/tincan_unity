#nullable enable
using NUnit.Framework;

namespace TinCan.Features.DayCycle.Tests
{
    public class DayClockTests
    {
        private static DayClock Clock(IDayClockDriver driver) => new(driver, SunPath.Default, new SunPathProcessor());

        [Test]
        public void FixedCycle_RunsAWholeDayEveryCycle_FromItsStartHour()
        {
            var clock = Clock(new FixedCycleDayDriver { CycleSeconds = 300f, StartHour = 7f });

            Assert.That(clock.HourAt(0), Is.EqualTo(7f).Within(1e-4f));
            Assert.That(clock.HourAt(150), Is.EqualTo(19f).Within(1e-4f));
            Assert.That(clock.HourAt(300), Is.EqualTo(7f).Within(1e-3f), "wraps into the next day");
        }

        [Test]
        public void FixedHour_StandsStill()
        {
            var clock = Clock(new FixedHourDayDriver { Hour = 14.5f });

            Assert.That(clock.HourAt(0), Is.EqualTo(14.5f));
            Assert.That(clock.HourAt(1e6), Is.EqualTo(14.5f));
        }

        [Test]
        public void Drivers_SwapWithoutTheClockKnowing()
        {
            var clock = Clock(new FixedHourDayDriver { Hour = 12f });
            Assert.That(clock.SunAt(0).Elevation, Is.EqualTo(12f).Within(0.01f));

            clock.Driver = new FixedHourDayDriver { Hour = 0f };

            Assert.That(clock.SunAt(0).Elevation, Is.EqualTo(-42f).Within(0.01f));
        }

        [Test]
        public void Voyage_RunsDawnToDuskByProgress()
        {
            var clock = Clock(new VoyageDayDriver { CastOffHour = 7f, ArrivalHour = 17f });

            Assert.That(clock.HourAt(new DayClockInput(999, sessionProgress: 0f)), Is.EqualTo(7f));
            Assert.That(clock.HourAt(new DayClockInput(0, sessionProgress: 0.5f)), Is.EqualTo(12f));
            Assert.That(clock.HourAt(new DayClockInput(0, sessionProgress: 1f)), Is.EqualTo(17f));
        }

        [Test]
        public void Voyage_WithoutASession_StandsAtCastOff()
        {
            var clock = Clock(new VoyageDayDriver { CastOffHour = 7f, ArrivalHour = 17f });

            Assert.That(clock.HourAt(123), Is.EqualTo(7f));
        }

        [Test]
        public void Config_WithoutADriver_StandsAtNoon()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<DayCycleConfig>();
            config.Driver = null;

            Assert.That(config.ActiveDriver.HourAt(new DayClockInput(123)), Is.EqualTo(12f));
            UnityEngine.Object.DestroyImmediate(config);
        }
    }
}
