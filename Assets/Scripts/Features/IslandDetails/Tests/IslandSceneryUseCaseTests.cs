#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.IslandDetails.Tests.Fakes;
using TinCan.Features.SkyIslands;
using UnityEngine;

namespace TinCan.Features.IslandDetails.Tests
{
    /// <summary><see cref="IslandSceneryUseCase"/>: every standing island gets its scenery, a few per frame, and loses it with the island.</summary>
    public class IslandSceneryUseCaseTests
    {
        private FakeSkyIslands _islands = null!;
        private FakeIslandSceneryBuilder _builder = null!;
        private IslandDetailsConfig _config = null!;
        private SkyIslandConfig _islandConfig = null!;

        [SetUp]
        public void SetUp()
        {
            _islands = new FakeSkyIslands();
            _builder = new FakeIslandSceneryBuilder();
            _config = IslandDetailsTestKit.Config();
            _islandConfig = ScriptableObject.CreateInstance<SkyIslandConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_islandConfig);
        }

        private IslandSceneryUseCase Create() =>
            new(_islands, _builder, new IslandDetailPlacementProcessor(), new SkyIslandSiteProcessor(), _islandConfig, _config);

        [Test]
        public void EveryStandingIsland_GetsItsScenery_OnePerFrame()
        {
            _islands.List.Add(IslandDetailsTestKit.Main(1));
            _islands.List.Add(IslandDetailsTestKit.Satellite(2));
            var scenery = Create();

            scenery.Tick();
            Assert.That(_builder.Built, Has.Count.EqualTo(1), "one island per frame");

            scenery.Tick();
            Assert.That(_builder.Built.Keys, Is.EquivalentTo(_islands.List.Select(island => island.Id)));
            Assert.That(scenery.Built, Has.Count.EqualTo(2));
        }

        [Test]
        public void TheBuilder_GetsTheIslandsBouldersAndBushes()
        {
            var island = IslandDetailsTestKit.Main(6);
            _islands.List.Add(island);

            Create().Tick();

            var details = _builder.Built[island.Id];
            Assert.That(details.Any(d => d.Kind == IslandDetailKind.Boulder), Is.True);
            Assert.That(details.Any(d => d.Kind == IslandDetailKind.Bush), Is.True);
        }

        [Test]
        public void AnIslandThatStreamsOut_LosesItsScenery_AndOneThatComesBack_GetsItAgain()
        {
            var island = IslandDetailsTestKit.Main(1);
            _islands.List.Add(island);
            var scenery = Create();
            scenery.Tick();

            _islands.List.Clear();
            scenery.Tick();
            Assert.That(_builder.Removed, Is.EqualTo(new[] { island.Id }));
            Assert.That(scenery.Built, Is.Empty);

            _islands.List.Add(island);
            scenery.Tick();
            Assert.That(_builder.Built.Keys, Is.EquivalentTo(new[] { island.Id }));
        }

        [Test]
        public void Disposing_ClearsTheBuilder()
        {
            _islands.List.Add(IslandDetailsTestKit.Main(1));
            var scenery = Create();
            scenery.Tick();

            scenery.Dispose();

            Assert.That(_builder.Clears, Is.EqualTo(1));
            Assert.That(scenery.Built, Is.Empty);
        }
    }
}
