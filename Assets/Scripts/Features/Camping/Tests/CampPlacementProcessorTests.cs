#nullable enable
using NUnit.Framework;
using UnityEngine;

namespace TinCan.Features.Camping.Tests
{
    public class CampPlacementProcessorTests
    {
        [TestCase(true, false, CampPlaceable.Campfire)]
        [TestCase(false, true, CampPlaceable.Tent)]
        public void Placing_FollowsWhatIsHeld(bool bundle, bool tent, CampPlaceable expected)
        {
            Assert.That(CampPlacementProcessor.Placing(bundle, tent), Is.EqualTo(expected));
        }

        [Test]
        public void Placing_NothingHeld_IsNothing()
        {
            Assert.That(CampPlacementProcessor.Placing(false, false), Is.Null);
        }

        [Test]
        public void Judge_FlatGroundWithRoom_Fits()
        {
            var verdict = CampPlacementProcessor.Judge(new CampGround(Vector3.zero, Vector3.up, false), 25f, () => true);

            Assert.That(verdict, Is.EqualTo(CampPlacementVerdict.Fits));
        }

        [Test]
        public void Judge_TheShip_IsRefused_BeforeAskingForRoom()
        {
            bool asked = false;
            var verdict = CampPlacementProcessor.Judge(new CampGround(Vector3.zero, Vector3.up, true), 25f, () => asked = true);

            Assert.That(verdict, Is.EqualTo(CampPlacementVerdict.OnTheShip));
            Assert.That(asked, Is.False);
        }

        [Test]
        public void Judge_GroundSteeperThanAllowed_IsTooSteep()
        {
            var slope = Quaternion.Euler(30f, 0f, 0f) * Vector3.up;

            Assert.That(CampPlacementProcessor.Judge(new CampGround(Vector3.zero, slope, false), 25f, () => true),
                Is.EqualTo(CampPlacementVerdict.TooSteep));
            Assert.That(CampPlacementProcessor.Judge(new CampGround(Vector3.zero, slope, false), 35f, () => true),
                Is.EqualTo(CampPlacementVerdict.Fits));
        }

        [Test]
        public void Judge_NoRoom_IsNoRoom()
        {
            Assert.That(CampPlacementProcessor.Judge(new CampGround(Vector3.zero, Vector3.up, false), 25f, () => false),
                Is.EqualTo(CampPlacementVerdict.NoRoom));
        }

        [Test]
        public void FacingBack_TurnsTheFrontTowardTheEye_Upright()
        {
            var rotation = CampPlacementProcessor.FacingBack(new Vector3(0f, 1.7f, 0f), new Vector3(3f, 0f, 0f));

            Assert.That(Vector3.Angle(rotation * Vector3.forward, Vector3.left), Is.LessThan(0.01f));
            Assert.That(Vector3.Angle(rotation * Vector3.up, Vector3.up), Is.LessThan(0.01f));
        }

        [Test]
        public void AimDirection_PositivePitch_LooksDown()
        {
            var direction = CampPlacementProcessor.AimDirection(Quaternion.identity, 30f);

            Assert.That(direction.y, Is.EqualTo(-0.5f).Within(1e-4f));
            Assert.That(direction.z, Is.GreaterThan(0f));
        }

        [Test]
        public void RoomLift_ClearsTheSteepestSlopeAcrossTheDisc()
        {
            float lift = CampPlacementProcessor.RoomLift(1f, 45f);

            Assert.That(lift, Is.GreaterThan(1f));
        }
    }
}
