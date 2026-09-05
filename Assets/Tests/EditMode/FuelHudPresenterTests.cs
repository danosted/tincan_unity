#nullable enable
using NUnit.Framework;
using TinCan.Features.Airship.Fuel;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    public class FuelHudPresenterTests
    {
        private FakeActorRegistry _registry = null!;
        private FakeHudValues _hud = null!;
        private FakeTimeService _time = null!;
        private FuelHudPresenter _presenter = null!;
        private FakeAirshipView? _airship;

        [SetUp]
        public void SetUp()
        {
            _registry = new FakeActorRegistry();
            _hud = new FakeHudValues();
            _time = new FakeTimeService();
            _presenter = new FuelHudPresenter(_registry, _hud, _time);
        }

        [TearDown]
        public void TearDown() => _airship?.Destroy();

        [Test]
        public void Tick_NoAirship_ShowsNothing()
        {
            _presenter.Tick();

            Assert.That(_hud.All.ContainsKey(FuelHudPresenter.HudKey), Is.False);
        }

        [Test]
        public void Tick_WithTank_ShowsRoundedLevel()
        {
            _airship = new FakeAirshipView();
            var tank = FakeFuelTankBehaviour.AttachTo(_airship.GameObject, null);
            tank.Inner.Level = 86.6f;
            _registry.Register(_airship);

            _presenter.Tick();

            Assert.That(_hud.All[FuelHudPresenter.HudKey], Is.EqualTo("87"));
        }

        [Test]
        public void Tick_AirshipAppearsLater_IsPickedUpAfterLookupInterval()
        {
            _presenter.Tick();
            _airship = new FakeAirshipView();
            FakeFuelTankBehaviour.AttachTo(_airship.GameObject, null);
            _registry.Register(_airship);

            _time.Time = 5f;
            _presenter.Tick();

            Assert.That(_hud.All[FuelHudPresenter.HudKey], Is.EqualTo("100"));
        }

        [Test]
        public void Tick_AirshipRemoved_ClearsValue()
        {
            _airship = new FakeAirshipView();
            FakeFuelTankBehaviour.AttachTo(_airship.GameObject, null);
            _registry.Register(_airship);
            _presenter.Tick();

            _registry.Unregister(_airship);
            _airship.Destroy();
            _airship = null;
            _time.Time = 5f;
            _presenter.Tick();

            Assert.That(_hud.All.ContainsKey(FuelHudPresenter.HudKey), Is.False);
        }
    }
}
