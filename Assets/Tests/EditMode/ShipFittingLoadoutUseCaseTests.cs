#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.ShipDesigns;
using TinCan.Features.ShipSockets;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipFittingLoadoutUseCase"/> (server): once a ship stands built from a design, the design's loadout is
    /// mounted, once per design; fittings the ship cannot take are skipped and the rest still mount.
    /// </summary>
    public class ShipFittingLoadoutUseCaseTests
    {
        private const string Cannon = "weapon.cannon";

        private sealed class FakeAssembly : IShipAssembly
        {
            public Dictionary<Guid, ulong> Built { get; } = new();

            public bool TryGetBuilt(Guid shipId, out ulong hash, out int builtParts)
            {
                builtParts = 0;
                return Built.TryGetValue(shipId, out hash);
            }
        }

        private sealed class RecordingFittings : IShipFittings
        {
            public List<MountedFitting> Mounted { get; } = new();
            public HashSet<ShipSocketId> Refuse { get; } = new();

            public bool Mount(Guid shipId, ShipSocketId socket, string fittingId, out string? error)
            {
                error = Refuse.Contains(socket) ? $"The ship has no {socket}." : null;
                if (error == null) Mounted.Add(new MountedFitting(socket, fittingId, 0));
                return error == null;
            }

            public bool Unmount(Guid shipId, ShipSocketId socket) => Mounted.RemoveAll(m => m.Socket.Equals(socket)) > 0;

            public IReadOnlyList<MountedFitting> MountedOn(Guid shipId) => Mounted;
        }

        private FakeActorRegistry _actors = null!;
        private FakeAirshipView _ship = null!;
        private FakeShipDesignState _state = null!;
        private FakeAssembly _assembly = null!;
        private RecordingFittings _fittings = null!;
        private ShipDesignsConfig _designs = null!;
        private ShipFittingLoadoutUseCase _loadouts = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _ship = new FakeAirshipView("Ship");
            _state = new FakeShipDesignState(_ship);
            _actors.Register(_ship);
            _actors.Register(_state);
            _assembly = new FakeAssembly();
            _fittings = new RecordingFittings();
            _designs = ScriptableObject.CreateInstance<ShipDesignsConfig>();
            var loadout = new ShipFittingLoadout(new[]
            {
                new ShipFittingPlacement(new ShipSocketId(3, 0), Cannon),
                new ShipFittingPlacement(new ShipSocketId(4, 0), Cannon),
            });
            var store = new FileShipFittingLoadoutStore(Path.Combine(Path.GetTempPath(), "TinCanNoLoadouts_" + Guid.NewGuid().ToString("N")),
                new Dictionary<string, string> { ["Galleon"] = ShipFittingLoadoutJsonCodec.Encode(loadout) });
            _loadouts = new ShipFittingLoadoutUseCase(_actors, new FakeNetworkService(), _assembly, store, _fittings, _designs, new FakeEventPublisher());
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            UnityEngine.Object.DestroyImmediate(_designs);
        }

        private void Fly(string designName, bool built = true)
        {
            _state.Set(ShipDesign.Empty(designName));
            if (built) _assembly.Built[_ship.Id] = _state.DesignHash;
        }

        [Test]
        public void OnceTheShipIsBuilt_TheDesignsLoadout_IsMounted_Once()
        {
            Fly("Galleon", built: false);
            _loadouts.Tick();
            Assert.That(_fittings.Mounted, Is.Empty, "not built yet");

            _assembly.Built[_ship.Id] = _state.DesignHash;
            _loadouts.Tick();
            _loadouts.Tick();

            Assert.That(_fittings.Mounted.Select(m => m.Socket.PartInstanceId), Is.EqualTo(new[] { 3, 4 }));
        }

        [Test]
        public void AFittingTheShipCannotTake_IsSkipped_TheRestMount()
        {
            _fittings.Refuse.Add(new ShipSocketId(3, 0));
            Fly("Galleon");

            _loadouts.Tick();

            Assert.That(_fittings.Mounted.Single().Socket, Is.EqualTo(new ShipSocketId(4, 0)));
        }

        [Test]
        public void ADesignWithoutALoadout_MountsNothing()
        {
            Fly("Starter");

            _loadouts.Tick();

            Assert.That(_fittings.Mounted, Is.Empty);
        }

        [Test]
        public void AFittingAlreadyInItsSocket_IsNotMountedAgain()
        {
            _fittings.Mounted.Add(new MountedFitting(new ShipSocketId(3, 0), Cannon, 0));
            Fly("Galleon");

            _loadouts.Tick();

            Assert.That(_fittings.Mounted.Select(m => m.Socket.PartInstanceId), Is.EqualTo(new[] { 3, 4 }));
        }
    }
}
