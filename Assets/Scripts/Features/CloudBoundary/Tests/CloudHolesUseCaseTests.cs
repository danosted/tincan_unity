#nullable enable
using System;
using NUnit.Framework;
using TinCan.Features.CloudBoundary.Tests.Fakes;
using TinCan.Tests.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Features.CloudBoundary.Tests
{
    public class CloudHolesUseCaseTests
    {
        private CloudBoundaryConfig _config = null!;
        private FakeActorRegistry _actors = null!;
        private FakeTimeService _time = null!;
        private FakeCloudHoleState _state = null!;
        private CloudHolesUseCase _holes = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CloudBoundaryConfig>();
            _actors = new FakeActorRegistry();
            _time = new FakeTimeService { Time = 100f };
            _state = new FakeCloudHoleState();
            _actors.Register(_state);
            _holes = new CloudHolesUseCase(new FakeNetworkService(), _actors, _time, new CloudHoleProcessor(), _config);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void TryOpen_AddsTheHoleToTheReplicatedList()
        {
            var hole = new CloudHole(new Vector2(5f, 6f), 40f, openedAt: 100f, openSeconds: 0f);

            Assert.That(_holes.TryOpen(hole), Is.True);
            Assert.That(_state.Holes, Is.EqualTo(new[] { hole }));
        }

        [Test]
        public void TryOpen_RefusesBeyondTheLimit()
        {
            for (int i = 0; i < ICloudHoles.MaxHoles; i++) _holes.TryOpen(new CloudHole(Vector2.zero, 10f, 0f, 0f));

            Assert.That(_holes.TryOpen(new CloudHole(Vector2.zero, 10f, 0f, 0f)), Is.False);
            Assert.That(_state.Holes.Count, Is.EqualTo(ICloudHoles.MaxHoles));
        }

        [Test]
        public void TryOpen_RefusesWithoutTheHoleState()
        {
            _state.IsSimulating = false;

            Assert.That(_holes.TryOpen(new CloudHole(Vector2.zero, 10f, 0f, 0f)), Is.False);
            Assert.That(_holes.Holes, Is.Empty);
        }

        [Test]
        public void ShapesNow_FollowTheSharedTime()
        {
            _holes.TryOpen(new CloudHole(Vector2.zero, 40f, openedAt: 100f, openSeconds: 10f));

            Assert.That(_holes.ShapesNow[0].Openness, Is.EqualTo(0f));
            _time.Time = 110f;
            Assert.That(_holes.ShapesNow[0].Openness, Is.EqualTo(1f));
            Assert.That(_holes.ShapesNow[0].Radius, Is.EqualTo(40f));
        }

        [Test]
        public void Tick_DropsHolesThatHaveShut()
        {
            _holes.TryOpen(new CloudHole(Vector2.zero, 10f, openedAt: 0f, openSeconds: 1f, closesAt: 50f, closeSeconds: 10f));
            _holes.TryOpen(new CloudHole(Vector2.one, 10f, openedAt: 0f, openSeconds: 1f));

            _holes.Tick();

            Assert.That(_state.Holes.Count, Is.EqualTo(1));
            Assert.That(_state.Holes[0].Centre, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void SurfaceQuery_DropsIntoAnOpenHole()
        {
            var query = new CloudSurfaceQuery(_config, _time, new ICloudHoles[] { _holes });
            _holes.TryOpen(new CloudHole(new Vector2(200f, 0f), 50f, openedAt: 0f, openSeconds: 0f));

            Assert.That(query.GetSurfaceHeight(0f, 0f), Is.EqualTo(_config.BaseAltitude));
            Assert.That(query.GetSurfaceHeight(200f, 0f), Is.EqualTo(_config.BaseAltitude - _config.HoleDepth));
        }

        [Test]
        public void SurfaceQuery_DropsIntoARiftOfTheWeb_AndStaysOnTheCloudBetween()
        {
            var sea = ScriptableObject.CreateInstance<CloudSeaConfig>();
            try
            {
                sea.WebOpenShare = 0.6f;
                var query = new CloudSurfaceQuery(_config, _time, new ICloudHoles[] { _holes }, new[] { sea });
                CloudWeb web = sea.Web(_config.WorldSeed);
                bool sawRift = false, sawCloud = false;
                for (int i = 0; i < 4000 && !(sawRift && sawCloud); i++)
                {
                    float x = (i % 64) * 53f, z = (i / 64) * 53f;
                    float cut = CloudSeaFormula.WebCut(x, z, _time.Time, web);
                    float height = query.GetSurfaceHeight(x, z);
                    Assert.That(height, Is.EqualTo(_config.BaseAltitude - cut * _config.HoleDepth).Within(1e-3f), $"at {x}, {z}");
                    sawRift |= cut > 0.99f;
                    sawCloud |= cut < 0.01f;
                }

                Assert.That(sawRift, Is.True, "no rift found");
                Assert.That(sawCloud, Is.True, "no cloud found");
            }
            finally
            {
                Object.DestroyImmediate(sea);
            }
        }

        [Test]
        public void SurfaceQuery_WithoutTheSea_IsTheTopologyAlone()
        {
            var query = new CloudSurfaceQuery(_config, _time, Array.Empty<ICloudHoles>());

            Assert.That(query.GetSurfaceHeight(200f, 0f), Is.EqualTo(_config.BaseAltitude));
        }
    }
}
