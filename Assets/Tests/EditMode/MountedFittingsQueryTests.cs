#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.ShipSockets;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>MountedFittingsQuery: the replicated mounts joined with the sockets this peer built.</summary>
    public class MountedFittingsQueryTests
    {
        private FakeAirshipView _ship = null!;

        [SetUp]
        public void SetUp() => _ship = new FakeAirshipView();

        [TearDown]
        public void TearDown() => _ship.Destroy();

        [Test]
        public void ListsMountedFittings_WithTheirMounts_InSocketOrder_SkippingUnbuiltSockets()
        {
            var actors = new FakeActorRegistry();
            var sockets = new FakeShipSocketList();
            var bow = FakeShipSockets.Socket(_ship.Transform, 20, new Vector3(0f, 0f, 5f));
            var hold = FakeShipSockets.Socket(_ship.Transform, 10, new Vector3(0f, -2f, 0f));
            sockets.Ships[_ship.Id] = new List<ShipSocketInfo> { bow, hold };

            var state = new FakeShipFittingState(_ship);
            state.ServerSetMounted(new[]
            {
                new MountedFitting(bow.Id, "weapon.cannon", 1),
                new MountedFitting(new ShipSocketId(99, 0), "weapon.cannon", 2), // its part is not built here
                new MountedFitting(hold.Id, "storage.tool_rack", 3),
            });
            actors.Register(state);

            var fittings = new MountedFittingsQuery(actors, sockets).FittingsOn(_ship.Id);

            Assert.That(fittings.Select(f => f.FittingId), Is.EqualTo(new[] { "storage.tool_rack", "weapon.cannon" }));
            Assert.That(fittings[1].Mount, Is.SameAs(bow.Mount));
        }

        [Test]
        public void ShipWithoutFittingState_HasNone()
        {
            var fittings = new MountedFittingsQuery(new FakeActorRegistry(), new FakeShipSocketList()).FittingsOn(_ship.Id);
            Assert.That(fittings, Is.Empty);
        }
    }
}
