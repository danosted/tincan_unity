#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Features.Airship.Fuel.Minigame;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class CatchProcessorTests
    {
        private readonly List<FakeFlyingCanView> _cans = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var can in _cans) can.Destroy();
            _cans.Clear();
        }

        [Test]
        public void NetPosition_IsInFrontOfPlayerIgnoringPitch()
        {
            var processor = new CatchProcessor();

            var net = processor.NetPosition(new Vector3(1f, 0f, 1f), new Vector3(0f, 0.7f, 0.7f), reach: 2f, height: 1f);

            Assert.That(net.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(net.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(net.z, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void TryFindCatchable_PicksNearestWithinRadius()
        {
            var processor = new CatchProcessor();
            var far = Spawn(new Vector3(0f, 0f, 10f));
            var near = Spawn(new Vector3(1f, 0f, 0f));
            var nearer = Spawn(new Vector3(0.5f, 0f, 0f));

            bool found = processor.TryFindCatchable(Vector3.zero, 2f, _cans, out var nearest);

            Assert.That(found, Is.True);
            Assert.That(nearest, Is.SameAs(nearer));
        }

        [Test]
        public void TryFindCatchable_NothingInRadius_ReturnsFalse()
        {
            var processor = new CatchProcessor();
            Spawn(new Vector3(0f, 0f, 5f));

            Assert.That(processor.TryFindCatchable(Vector3.zero, 2f, _cans, out var nearest), Is.False);
            Assert.That(nearest, Is.Null);
        }

        private FakeFlyingCanView Spawn(Vector3 position)
        {
            var can = new FakeFlyingCanView(position);
            _cans.Add(can);
            return can;
        }
    }
}
