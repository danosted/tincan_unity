#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Interaction;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.Anchor.Tests
{
    /// <summary>The anchor's hold (<see cref="AnchorHoldUseCase"/>) and its Interact toggle.</summary>
    public class AnchorTests
    {
        private sealed class FakeAnchor : IAnchor
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating { get; set; } = true;
            public Transform? Ship { get; set; }
            public bool IsDown { get; private set; }
            public AnchorLadder? Ladder => null;
            public void ServerSetDown(bool down) => IsDown = down;
            public bool ServerToggle() => IsDown = !IsDown;
        }

        private sealed class Player : TinCan.Core.Domain.IActor
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
        }

        private FakeActorRegistry _actors = null!;
        private FakeAirshipView _ship = null!;
        private FakeAirshipView _otherShip = null!;
        private FakeAnchor _anchor = null!;
        private AnchorHoldUseCase _hold = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _ship = new FakeAirshipView();
            _otherShip = new FakeAirshipView();
            _anchor = new FakeAnchor { Ship = _ship.Transform };
            _actors.Register(_anchor);
            _hold = new AnchorHoldUseCase(_actors);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            _otherShip.Destroy();
        }

        [Test]
        public void AnAnchorThatIsUp_HoldsNothing() => Assert.That(_hold.Holds(_ship), Is.False);

        [Test]
        public void ADroppedAnchor_HoldsItsOwnShip_AndNoOther()
        {
            _anchor.ServerSetDown(true);

            Assert.That(_hold.Holds(_ship), Is.True);
            Assert.That(_hold.Holds(_otherShip), Is.False);
        }

        [Test]
        public void ADespawnedAnchor_HoldsNothing()
        {
            _anchor.ServerSetDown(true);
            _anchor.IsSimulating = false;

            Assert.That(_hold.Holds(_ship), Is.False);
        }

        [Test]
        public void Interact_DropsTheAnchor_ThenRaisesIt()
        {
            var handler = new ToggleInteractionHandler(new FakeEventPublisher());

            handler.Handle(new InteractionContext(new Player(), _anchor, null!));
            Assert.That(_anchor.IsDown, Is.True);

            handler.Handle(new InteractionContext(new Player(), _anchor, null!));
            Assert.That(_anchor.IsDown, Is.False);
        }
    }
}
