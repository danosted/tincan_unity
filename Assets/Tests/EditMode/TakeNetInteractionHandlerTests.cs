#nullable enable
using NUnit.Framework;
using TinCan.Features.Carry;
using TinCan.Features.Interaction;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    public class TakeNetInteractionHandlerTests
    {
        private TakeNetInteractionHandler _handler = null!;
        private FakeNetRack _rack = null!;
        private FakeCarrierActor _player = null!;

        [SetUp]
        public void SetUp()
        {
            _handler = new TakeNetInteractionHandler(new FakeEventPublisher());
            _rack = new FakeNetRack();
            _player = new FakeCarrierActor();
        }

        [Test]
        public void Handle_EmptyHanded_TakesNet()
        {
            _handler.Handle(new InteractionContext(_player, _rack, null!));

            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.Net));
        }

        [Test]
        public void Handle_CarryingNet_ReturnsIt()
        {
            _player.Carried = CarriedItem.Net;

            _handler.Handle(new InteractionContext(_player, _rack, null!));

            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.None));
        }

        [Test]
        public void Handle_CarryingJerryCan_IsRefused()
        {
            _player.Carried = CarriedItem.JerryCan;

            _handler.Handle(new InteractionContext(_player, _rack, null!));

            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.JerryCan));
        }

        [Test]
        public void Handle_TargetNotARack_IsIgnored()
        {
            _handler.Handle(new InteractionContext(_player, new FakeJerryCanSupply(), null!));

            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.None));
        }
    }
}
