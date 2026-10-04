#nullable enable
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Features;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipDesignUseCase"/>: the server gives ships the selected design (launch argument, else the config's
    /// default), refuses invalid ones, and <see cref="ShipDesignFixtureFilter"/> keeps the helm fixture off designed ships.
    /// </summary>
    public class ShipDesignUseCaseTests
    {
        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private ShipDesignTestParts _parts = null!;
        private ShipDesignsConfig _config = null!;
        private FakeActorRegistry _actors = null!;
        private FakeAirshipView _ship = null!;
        private FakeShipDesignState _state = null!;
        private RecordingPublisher _events = null!;
        private Dictionary<string, string> _builtIns = null!;
        private readonly ShipDesignJsonCodec _codec = new(ShipDesignLimits.Default);

        [SetUp]
        public void SetUp()
        {
            _parts = new ShipDesignTestParts();
            _config = ScriptableObject.CreateInstance<ShipDesignsConfig>();
            _config.DefaultDesign = "Starter";
            _actors = new FakeActorRegistry();
            _ship = new FakeAirshipView("Ship");
            _state = new FakeShipDesignState(_ship);
            _actors.Register(_ship);
            _actors.Register(_state);
            _events = new RecordingPublisher();
            _builtIns = new Dictionary<string, string>
            {
                ["Starter"] = _codec.Encode(Design((Helm, 0, 0, 0, 0), (Block, 0, -1, 0, 0))),
                ["Other"] = _codec.Encode(Design((Helm, 0, 0, 0, 0))),
                ["Broken"] = _codec.Encode(Design((Block, 0, 0, 0, 0))),
            };
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            _parts.Dispose();
            Object.DestroyImmediate(_config);
        }

        private ShipDesignUseCase UseCase(params string[] launchArguments) =>
            new(_actors, new FakeNetworkService(), _parts.Catalog,
                new FileShipDesignStore(Path.Combine(Path.GetTempPath(), "TinCanNoShips_" + System.Guid.NewGuid().ToString("N")), _builtIns, _codec, 100_000),
                new ShipDesignValidator(), _config, _events, new ShipStatsProcessor(), launchArguments);

        [Test]
        public void AShipWithoutADesign_GetsTheDefault_Once()
        {
            var designs = UseCase();

            designs.Tick();
            designs.Tick();

            Assert.That(_state.Writes, Is.EqualTo(1));
            var applied = (ShipDesignAppliedEvent)_events.Events.Find(e => e is ShipDesignAppliedEvent)!;
            Assert.That((applied.ShipId, applied.Parts), Is.EqualTo((_ship.Id, 2)));
            Assert.That(_state.DesignHash, Is.EqualTo(applied.Hash));
            var decoded = ShipDesignBinaryCodec.Decode(_state.DesignBytes, ShipDesignLimits.Default).Design!;
            Assert.That(ShipDesignHash.Compute(decoded), Is.EqualTo(applied.Hash));
        }

        [Test]
        public void TheLaunchArgument_ChoosesTheDesign()
        {
            UseCase("-autohost", "-shipDesign", "Other").Tick();

            Assert.That(_state.DesignHash, Is.EqualTo(ShipDesignHash.Compute(_codec.Decode(_builtIns["Other"]).Design!)));
        }

        [Test]
        public void ASelectedDesign_WinsOverTheStore()
        {
            var designs = UseCase();
            var chosen = Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 0), (Block, 2, 0, 0, 0));

            designs.Select(chosen);
            designs.Tick();

            Assert.That(_state.DesignHash, Is.EqualTo(ShipDesignHash.Compute(chosen)));
        }

        [Test]
        public void AnInvalidDesign_IsRefused_AndTheShipStaysBare()
        {
            var designs = UseCase("-shipDesign", "Broken");

            designs.Tick();
            designs.Tick();

            Assert.That(_state.Writes, Is.Zero);
            Assert.That(designs.TryApply(_state, _codec.Decode(_builtIns["Broken"]).Design!, out var error), Is.False);
            Assert.That(error, Does.Contain("MissingCore"));
        }

        [Test]
        public void AMissingDesign_LeavesTheShipBare()
        {
            UseCase("-shipDesign", "Nope").Tick();

            Assert.That(_state.Writes, Is.Zero);
        }

        [Test]
        public void AStateNotYetOnAShip_Waits()
        {
            _state.Ship = null;
            var designs = UseCase();
            designs.Tick();
            Assert.That(_state.Writes, Is.Zero);

            _state.Ship = _ship;
            designs.Tick();
            Assert.That(_state.Writes, Is.EqualTo(1));
        }

        [Test]
        public void TheFixtureFilter_KeepsOffFixturesAPartSpawns()
        {
            var filter = new ShipDesignFixtureFilter(_parts.Catalog);
            var helm = ScriptableObject.CreateInstance<ShipFixtureDefinition>();
            var other = ScriptableObject.CreateInstance<ShipFixtureDefinition>();
            var otherPrefab = new GameObject("Other");
            try
            {
                helm.Prefab = _parts.HelmPrefab;
                other.Prefab = otherPrefab;

                Assert.That(filter.ShouldSpawn(_ship, helm), Is.False);
                Assert.That(filter.ShouldSpawn(_ship, other), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(helm);
                Object.DestroyImmediate(other);
                Object.DestroyImmediate(otherPrefab);
            }
        }
    }
}
