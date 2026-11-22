#nullable enable
using NUnit.Framework;
using TinCan.Features.Camping.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.Camping.Tests
{
    public class CampPlacementFinderTests
    {
        private CampingConfig _config = null!;
        private FakeCampPlacementQuery _query = null!;
        private GameObject _body = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CampingConfig>();
            _query = new FakeCampPlacementQuery();
            _body = new GameObject("Body");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_body);
        }

        private CampPlacement Find(CampPlaceable kind) =>
            new CampPlacementFinder(_query, _config).Find(kind, new Vector3(0f, 1.7f, 0f), Vector3.forward, _body.transform);

        [Test]
        public void FlatGroundWithRoom_Fits_FacingThePlayer()
        {
            var placement = Find(CampPlaceable.Campfire);

            Assert.That(placement.Fits, Is.True);
            Assert.That(placement.Point, Is.EqualTo(new Vector3(0f, 0f, 2f)));
            Assert.That(Vector3.Angle(placement.Rotation * Vector3.forward, Vector3.back), Is.LessThan(0.01f));
        }

        [Test]
        public void NoGroundInReach_ShowsNothing()
        {
            _query.Ground = null;

            var placement = Find(CampPlaceable.Tent);

            Assert.That(placement.Verdict, Is.EqualTo(CampPlacementVerdict.NoGroundInReach));
            Assert.That(placement.HasGround, Is.False);
        }

        [Test]
        public void TheRoomAsked_IsTheKindsOwn()
        {
            Find(CampPlaceable.Tent);
            Assert.That(_query.LastRadius, Is.EqualTo(_config.TentRadius));

            Find(CampPlaceable.Campfire);
            Assert.That(_query.LastRadius, Is.EqualTo(_config.CampfireRadius));
        }

        [Test]
        public void SomethingInTheWay_DoesNotFit_ButStillShows()
        {
            _query.Room = false;

            var placement = Find(CampPlaceable.Campfire);

            Assert.That(placement.Verdict, Is.EqualTo(CampPlacementVerdict.NoRoom));
            Assert.That(placement.HasGround, Is.True);
        }
    }
}
