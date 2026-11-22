#nullable enable
using NUnit.Framework;
using TinCan.Core.Interaction;
using TinCan.Features.Camping.Tests.Fakes;
using TinCan.Tests.Fakes;

namespace TinCan.Features.Camping.Tests
{
    public class StokeFireInteractionHandlerTests
    {
        private CampingTestKit _kit = null!;
        private StokeFireInteractionHandler _handler = null!;
        private FakeFire _fire = null!;
        private FakeEquipmentActor _player = null!;

        [SetUp]
        public void SetUp()
        {
            _kit = new CampingTestKit();
            _handler = new StokeFireInteractionHandler(_kit.Events);
            _fire = new FakeFire { Config = _kit.Config };
            _player = new FakeEquipmentActor();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        private void Interact() => _handler.Handle(new InteractionContext(_player, _fire, null!));

        [Test]
        public void HoldingFirewood_LightsTheFire_AndUsesTheLog()
        {
            _player.Held = _kit.Firewood;

            Interact();

            Assert.That(_fire.BurnSecondsLeft, Is.EqualTo(180f));
            Assert.That(_player.Held, Is.Null);
            Assert.That(_kit.Events.Events, Has.Exactly(1).TypeOf<FireStokedEvent>());
        }

        [Test]
        public void FullFire_RefusesTheLog_AndYouKeepIt()
        {
            _fire.BurnSecondsLeft = 590f;
            _player.Held = _kit.Firewood;

            Interact();

            Assert.That(_player.Held, Is.SameAs(_kit.Firewood));
            Assert.That(_kit.Events.Events, Has.None.TypeOf<FireStokedEvent>());
        }

        [Test]
        public void EmptyHanded_DoesNotStoke()
        {
            Interact();

            Assert.That(_fire.StokeCalls, Is.EqualTo(0));
            Assert.That(_fire.BurnSecondsLeft, Is.EqualTo(0f));
        }

        [Test]
        public void HoldingSomethingElse_DoesNotStoke()
        {
            _player.Held = _kit.Other;

            Interact();

            Assert.That(_fire.StokeCalls, Is.EqualTo(0));
            Assert.That(_player.Held, Is.SameAs(_kit.Other));
        }
    }
}
