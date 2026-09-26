#nullable enable
using NUnit.Framework;
using TinCan.Features.Airship.Damage;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    public class ShipDamageHudPresenterTests
    {
        private FakeActorRegistry _actors = null!;
        private FakeHudValues _hud = null!;
        private FakeAirshipView _airship = null!;
        private FakeShipDamagePoint[] _points = null!;
        private readonly System.Collections.Generic.List<UnityEngine.Object> _assets = new();
        private ShipDamageHudPresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _hud = new FakeHudValues();
            _airship = new FakeAirshipView();
            var health = UnityEngine.ScriptableObject.CreateInstance<TinCan.Features.Abilities.HealthAttribute>();
            health.name = "Attr_Health";
            var maxHealth = UnityEngine.ScriptableObject.CreateInstance<TinCan.Features.Abilities.MaxHealthAttribute>();
            maxHealth.name = "Attr_MaxHealth";
            _assets.Add(health);
            _assets.Add(maxHealth);
            _points = FakeShipDamage.AttachPoints(_airship.GameObject, 3, health, maxHealth);
            _actors.Register(_airship);
            _presenter = new ShipDamageHudPresenter(_actors, _hud);
        }

        [TearDown]
        public void TearDown()
        {
            _airship.Destroy();
            foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void WholeHull_ShowsNothing()
        {
            _presenter.Tick();

            Assert.That(_hud.All.ContainsKey(ShipDamageHudPresenter.HudKey), Is.False);
        }

        [Test]
        public void BrokenParts_AreCounted_AndTheLineGoesAwayWhenRepaired()
        {
            _points[0].SetHealth01(0f);
            _points[2].SetHealth01(0.5f);
            _presenter.Tick();
            Assert.That(_hud.All[ShipDamageHudPresenter.HudKey], Is.EqualTo("2"));

            _points[0].SetHealth01(1f);
            _points[2].SetHealth01(1f);
            _presenter.Tick();
            Assert.That(_hud.All.ContainsKey(ShipDamageHudPresenter.HudKey), Is.False);
        }
    }
}
