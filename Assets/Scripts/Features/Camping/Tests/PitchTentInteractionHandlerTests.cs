#nullable enable
using NUnit.Framework;
using TinCan.Core.Interaction;
using TinCan.Features.Camping.Tests.Fakes;
using TinCan.Tests.Fakes;

namespace TinCan.Features.Camping.Tests
{
    public class PitchTentInteractionHandlerTests
    {
        private CampingTestKit _kit = null!;
        private PitchTentInteractionHandler _handler = null!;
        private FakeTentSpot _spot = null!;
        private FakeEquipmentActor _player = null!;

        [SetUp]
        public void SetUp()
        {
            _kit = new CampingTestKit();
            _handler = new PitchTentInteractionHandler(_kit.Events);
            _spot = new FakeTentSpot { Config = _kit.Config };
            _player = new FakeEquipmentActor();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        private void Interact() => _handler.Handle(new InteractionContext(_player, _spot, null!));

        [Test]
        public void HoldingATentRoll_PitchesTheTent()
        {
            _player.Held = _kit.Tent;

            Interact();

            Assert.That(_spot.IsPitched, Is.True);
            Assert.That(_player.Held, Is.Null);
            Assert.That(_kit.Events.Events, Has.Exactly(1).TypeOf<TentPitchedEvent>());
        }

        [Test]
        public void EmptyHandedAtAPitchedTent_PacksItIntoARoll()
        {
            _spot.IsPitched = true;

            Interact();

            Assert.That(_spot.IsPitched, Is.False);
            Assert.That(_player.Held, Is.SameAs(_kit.Tent));
            Assert.That(_kit.Events.Events, Has.Exactly(1).TypeOf<TentPackedEvent>());
            Assert.That(_spot.Removed, Is.False, "a tent spot stays");
        }

        [Test]
        public void PackingAPlacedTent_TakesItAway()
        {
            _spot.IsPitched = true;
            _spot.RemovesWhenPacked = true;

            Interact();

            Assert.That(_player.Held, Is.SameAs(_kit.Tent));
            Assert.That(_spot.Removed, Is.True);
        }

        [Test]
        public void APlacedTent_ThatCannotBePacked_Stays()
        {
            _spot.IsPitched = true;
            _spot.RemovesWhenPacked = true;
            _player.Held = _kit.Other;

            Interact();

            Assert.That(_spot.Removed, Is.False);
            Assert.That(_spot.IsPitched, Is.True);
        }

        [Test]
        public void EmptyHandedAtAFreeSpot_DoesNothing()
        {
            Interact();

            Assert.That(_spot.IsPitched, Is.False);
            Assert.That(_player.Held, Is.Null);
        }

        [Test]
        public void HandsFullAtAPitchedTent_LeavesItStanding()
        {
            _spot.IsPitched = true;
            _player.Held = _kit.Other;

            Interact();

            Assert.That(_spot.IsPitched, Is.True);
            Assert.That(_player.Held, Is.SameAs(_kit.Other));
        }

        [Test]
        public void HoldingATentRollAtAPitchedTent_KeepsTheRoll()
        {
            _spot.IsPitched = true;
            _player.Held = _kit.Tent;

            Interact();

            Assert.That(_spot.IsPitched, Is.True);
            Assert.That(_player.Held, Is.SameAs(_kit.Tent));
        }
    }
}
