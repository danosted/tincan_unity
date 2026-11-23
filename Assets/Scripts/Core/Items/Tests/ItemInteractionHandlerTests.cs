#nullable enable
using NUnit.Framework;
using TinCan.Core.Interaction;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Core.Items.Tests
{
    /// <summary>
    /// The one item handler (<see cref="ItemInteractionHandler"/>) on targets by their capabilities: a rack that never
    /// runs out, a counted source that takes things back (a woodpile, a bin), one that does not (a vein), consumers (a
    /// receiver, a fire), and a tent spot that is both.
    /// </summary>
    public class ItemInteractionHandlerTests
    {
        /// <summary>A target with whatever capabilities a test switches on.</summary>
        private sealed class Target : IItemYield, IItemStore, IItemConsumer
        {
            public ItemDefinition? Gives;
            public bool TakesBack;
            public ItemDefinition? Burns;
            public int Count;
            public int Capacity = 3;
            public bool Unlimited;
            public bool Room = true;
            public int Consumed;

            public ItemDefinition? YieldItem => Gives;
            public ItemDefinition? StoreItem => TakesBack ? Gives : null;

            public bool TryYield()
            {
                if (Unlimited) return true;
                if (Count <= 0) return false;
                Count--;
                return true;
            }

            public void Unyield() => Count++;

            public bool TryStore()
            {
                if (!Unlimited && Count >= Capacity) return false;
                Count++;
                return true;
            }

            public bool Accepts(ItemDefinition item) => Burns != null && item == Burns;

            public bool TryConsume(ItemDefinition item)
            {
                if (!Room) return false;
                Consumed++;
                return true;
            }
        }

        private ItemInteractionHandler _handler = null!;
        private FakeEquipmentActor _player = null!;
        private ItemDefinition _crystal = null!;
        private ItemDefinition _log = null!;

        [SetUp]
        public void SetUp()
        {
            _handler = new ItemInteractionHandler(new FakeEventPublisher());
            _player = new FakeEquipmentActor();
            _crystal = ItemDefinition.Create(8, "ITEM_FuelCrystal");
            _log = ItemDefinition.Create(5, "ITEM_Firewood");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_crystal);
            Object.DestroyImmediate(_log);
        }

        private void Interact(Target target) => _handler.Handle(new InteractionContext(_player, target, null!));

        [Test]
        public void EmptyHands_TakeOne_FromASource()
        {
            var vein = new Target { Gives = _crystal, Count = 2 };

            Interact(vein);

            Assert.That(_player.Held, Is.SameAs(_crystal));
            Assert.That(vein.Count, Is.EqualTo(1));
        }

        [Test]
        public void ABareSource_GivesNothing()
        {
            var vein = new Target { Gives = _crystal, Count = 0 };

            Interact(vein);

            Assert.That(_player.Held, Is.Null);
        }

        [Test]
        public void ARack_NeverRunsOut_AndTakesItsItemBack()
        {
            var rack = new Target { Gives = _log, TakesBack = true, Unlimited = true };

            Interact(rack);
            Assert.That(_player.Held, Is.SameAs(_log));

            Interact(rack);
            Assert.That(_player.Held, Is.Null);
        }

        [Test]
        public void HoldingItsItem_GoesBackIn_WhereItIsTakenBack_AndNotWhereItIsNot()
        {
            var pile = new Target { Gives = _log, TakesBack = true, Count = 1 };
            var vein = new Target { Gives = _log, Count = 1 };

            _player.Held = _log;
            Interact(vein);
            Assert.That(_player.Held, Is.SameAs(_log), "a vein takes nothing back");

            Interact(pile);
            Assert.That(_player.Held, Is.Null);
            Assert.That(pile.Count, Is.EqualTo(2));
        }

        [Test]
        public void AFullStore_KeepsTheItemInHand()
        {
            var bin = new Target { Gives = _crystal, TakesBack = true, Count = 3, Capacity = 3 };
            _player.Held = _crystal;

            Interact(bin);

            Assert.That(_player.Held, Is.SameAs(_crystal));
            Assert.That(bin.Count, Is.EqualTo(3));
        }

        [Test]
        public void AConsumer_UsesAHeldItemItAccepts()
        {
            var fire = new Target { Burns = _log };
            _player.Held = _log;

            Interact(fire);

            Assert.That(_player.Held, Is.Null);
            Assert.That(fire.Consumed, Is.EqualTo(1));
        }

        [Test]
        public void AConsumerWithoutRoom_LeavesTheItemInHand()
        {
            var tank = new Target { Burns = _crystal, Room = false };
            _player.Held = _crystal;

            Interact(tank);

            Assert.That(_player.Held, Is.SameAs(_crystal));
        }

        [Test]
        public void AnItemNobodyWants_StaysInHand()
        {
            var fire = new Target { Burns = _log, Gives = _log, Count = 3 };
            _player.Held = _crystal;

            Interact(fire);

            Assert.That(_player.Held, Is.SameAs(_crystal));
            Assert.That(fire.Count, Is.EqualTo(3));
        }

        [Test]
        public void ATakeThatCannotBeHandedOver_GoesBack()
        {
            var vein = new Target { Gives = _crystal, Count = 1 };
            _player.RefuseEquip = true;

            Interact(vein);

            Assert.That(vein.Count, Is.EqualTo(1));
        }
    }
}
