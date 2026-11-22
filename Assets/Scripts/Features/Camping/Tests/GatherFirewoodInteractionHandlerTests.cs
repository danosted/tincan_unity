#nullable enable
using NUnit.Framework;
using TinCan.Core.Interaction;
using TinCan.Features.Camping.Tests.Fakes;
using TinCan.Tests.Fakes;

namespace TinCan.Features.Camping.Tests
{
    public class GatherFirewoodInteractionHandlerTests
    {
        private CampingTestKit _kit = null!;
        private GatherFirewoodInteractionHandler _handler = null!;
        private FakeDeadwood _pile = null!;
        private FakeEquipmentActor _player = null!;

        [SetUp]
        public void SetUp()
        {
            _kit = new CampingTestKit();
            _handler = new GatherFirewoodInteractionHandler(_kit.Events);
            _pile = new FakeDeadwood { Config = _kit.Config, Available = 3 };
            _player = new FakeEquipmentActor();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        private void Interact() => _handler.Handle(new InteractionContext(_player, _pile, null!));

        [Test]
        public void EmptyHanded_TakesALog()
        {
            Interact();

            Assert.That(_player.Held, Is.SameAs(_kit.Firewood));
            Assert.That(_pile.Available, Is.EqualTo(2));
            Assert.That(_kit.Events.Events, Has.Exactly(1).TypeOf<FirewoodGatheredEvent>());
        }

        [Test]
        public void HoldingFirewood_PutsItBack()
        {
            _player.Held = _kit.Firewood;

            Interact();

            Assert.That(_player.Held, Is.Null);
            Assert.That(_pile.Available, Is.EqualTo(4));
        }

        [Test]
        public void BarePile_GivesNothing()
        {
            _pile.Available = 0;

            Interact();

            Assert.That(_player.Held, Is.Null);
            Assert.That(_kit.Events.Events, Has.None.TypeOf<FirewoodGatheredEvent>());
        }

        [Test]
        public void HandsFull_TakesNothing()
        {
            _player.Held = _kit.Other;

            Interact();

            Assert.That(_player.Held, Is.SameAs(_kit.Other));
            Assert.That(_pile.Available, Is.EqualTo(3));
        }
    }
}
