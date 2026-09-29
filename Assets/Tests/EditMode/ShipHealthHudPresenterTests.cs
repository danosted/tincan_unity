#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Gas;
using TinCan.Features.Airship.Damage;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipHealthHudPresenter"/>: the ship's replicated health as a HUD meter, on every peer.</summary>
    public class ShipHealthHudPresenterTests
    {
        private HealthAttribute _health = null!;
        private MaxHealthAttribute _maxHealth = null!;
        private FakeActorRegistry _actors = null!;
        private FakeHudValues _hud = null!;
        private FakeAbilityController _controller = null!;
        private FakeAirshipView _ship = null!;

        [SetUp]
        public void SetUp()
        {
            // Named: attributes are keyed by name, so two unnamed ones would be the same attribute.
            _health = ScriptableObject.CreateInstance<HealthAttribute>();
            _health.name = "Attr_Health";
            _maxHealth = ScriptableObject.CreateInstance<MaxHealthAttribute>();
            _maxHealth.name = "Attr_MaxHealth";
            _actors = new FakeActorRegistry();
            _hud = new FakeHudValues();
            _controller = new FakeAbilityController();
            _ship = new FakeAirshipView("Airship", _controller);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            Object.DestroyImmediate(_health);
            Object.DestroyImmediate(_maxHealth);
        }

        [Test]
        public void ShowsTheShipsHealthAsAFill()
        {
            var set = new HealthAttributeSet(_controller, _health, _maxHealth);
            set.InitializeBaseValues(1000f);
            _controller.RegisterAttributeSet(set);
            _controller.SetAttribute(_health, new AttributeValue(750f));
            _actors.Register(_ship);

            new ShipHealthHudPresenter(_actors, _hud).Tick();

            Assert.That(_hud.Meters[ShipHealthHudPresenter.MeterKey], Is.EqualTo(0.75f).Within(1e-4f));
        }

        [Test]
        public void NoShip_OrNoHealthYet_HidesTheMeter()
        {
            var presenter = new ShipHealthHudPresenter(_actors, _hud);
            _hud.SetMeter(ShipHealthHudPresenter.MeterKey, 1f);

            presenter.Tick();
            Assert.That(_hud.Meters.ContainsKey(ShipHealthHudPresenter.MeterKey), Is.False, "no ship");

            _actors.Register(_ship);
            _controller.RegisterAttributeSet(new HealthAttributeSet(_controller, _health, _maxHealth));
            presenter.Tick();
            Assert.That(_hud.Meters.ContainsKey(ShipHealthHudPresenter.MeterKey), Is.False, "health not seeded yet");
        }
    }
}
