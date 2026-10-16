#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Interaction;
using TinCan.Features.Respawn;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class CrankWinchInteractionHandlerTests
    {
        private LeashStore _leashes = null!;
        private Point _point = null!;
        private Winch _winch = null!;
        private InteractionDefinition _definition = null!;

        [SetUp]
        public void SetUp()
        {
            _leashes = new LeashStore();
            _point = new Point();
            _winch = new Winch { Point = _point };
            _definition = ScriptableObject.CreateInstance<InteractionDefinition>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_definition);

        [Test]
        public void Cranking_StartsAndStopsTheReel_WhileSomeoneHangsOn()
        {
            _leashes.Attach(Guid.NewGuid(), _point, 12f);
            var handler = new CrankWinchInteractionHandler(_leashes, new FakeEventPublisher());

            handler.Handle(Context());
            Assert.That(_leashes.IsWinching(_point), Is.True);

            handler.Handle(Context());
            Assert.That(_leashes.IsWinching(_point), Is.False);
        }

        [Test]
        public void WithNobodyOnTheLeash_TheWinchStaysStill()
        {
            new CrankWinchInteractionHandler(_leashes, new FakeEventPublisher()).Handle(Context());

            Assert.That(_leashes.IsWinching(_point), Is.False);
        }

        private InteractionContext Context() => new(new FakeEquipmentActor(), _winch, _definition);

        private sealed class Point : ILeashPoint
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public Transform Anchor => null!;
            public Transform Stand => null!;
            public Transform? Ship => null;
        }

        private sealed class Winch : ILeashWinch
        {
            public ILeashPoint? Point { get; set; }
        }
    }
}
