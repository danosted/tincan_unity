#nullable enable
using NUnit.Framework;
using TinCan.Features.Airship.Damage;
using TinCan.Features.DesignedEvents;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    /// <summary>The designed-event handlers: AnnounceActionHandler, BreakShipPartActionHandler, BrokenPartsAtMostConditionHandler.</summary>
    public class EventActionHandlerTests
    {
        [Test]
        public void Announce_SetsTheHudEventLine()
        {
            var hud = new FakeHudValues();
            Assert.That(new AnnounceActionHandler(hud).Execute(new Announce("hello")), Is.True);
            Assert.That(hud.All[AnnounceActionHandler.HudKey], Is.EqualTo("hello"));
        }

        [Test]
        public void BreakShipPart_BreaksThatPart_AndReportsAMissingOne()
        {
            var breakage = new FakeShipBreakage { PartCount = 3 };
            var handler = new BreakShipPartActionHandler(breakage);

            Assert.That(handler.Execute(new BreakShipPart(2)), Is.True);
            Assert.That(breakage.Broken, Is.EquivalentTo(new[] { 2 }));
            Assert.That(handler.Execute(new BreakShipPart(9)), Is.False);
        }

        [Test]
        public void Handler_IgnoresOtherActionTypes()
        {
            var breakage = new FakeShipBreakage();
            Assert.That(new BreakShipPartActionHandler(breakage).Execute(new Announce("x")), Is.False);
            Assert.That(breakage.BrokenCount, Is.Zero);
        }

        [Test]
        public void BrokenPartsAtMost_ComparesWithBrokenCount()
        {
            var breakage = new FakeShipBreakage();
            var handler = new BrokenPartsAtMostConditionHandler(breakage);

            Assert.That(handler.IsMet(new BrokenPartsAtMost(0)), Is.True);
            breakage.TryBreak(0);
            breakage.TryBreak(1);
            Assert.That(handler.IsMet(new BrokenPartsAtMost(0)), Is.False);
            Assert.That(handler.IsMet(new BrokenPartsAtMost(2)), Is.True);
        }

        [Test]
        public void Registry_RoutesByType_AndListsMissingHandlers()
        {
            var breakage = new FakeShipBreakage();
            var registry = new EventHandlerRegistry(
                new IEventActionHandler[] { new BreakShipPartActionHandler(breakage) },
                new IEventConditionHandler[0]);
            var definition = new EventDefinition.Builder(1, "Test")
                .Phase("A", p => p.OnEnter(new BreakShipPart(1), new Announce("x")).Until(new BrokenPartsAtMost(0)).Timeout(1f))
                .Build();

            Assert.That(registry.TryExecute(new BreakShipPart(1)), Is.True);
            Assert.That(registry.TryExecute(new Announce("x")), Is.False);
            Assert.That(registry.IsMet(new BrokenPartsAtMost(5)), Is.False, "no handler: never met");
            Assert.That(registry.MissingHandlers(definition), Is.EquivalentTo(new[] { typeof(Announce), typeof(BrokenPartsAtMost) }));
        }
    }
}
