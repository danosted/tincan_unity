#nullable enable
using NUnit.Framework;
using TinCan.Core.Ship;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="AirshipBoardingPose"/>. Plan: <c>crew-gate-and-boarding.md</c>.</summary>
    public class AirshipBoardingPoseTests
    {
        private FakeAirshipView _ship = null!;

        [SetUp]
        public void SetUp()
        {
            _ship = new FakeAirshipView("Airship");
            _ship.Transform.SetPositionAndRotation(new Vector3(100f, 50f, 0f), Quaternion.Euler(0f, 90f, 0f));
        }

        [TearDown]
        public void TearDown() => _ship.Destroy();

        [Test]
        public void WithoutARespawnPoint_UsesTheOffset_InShipSpace()
        {
            var (position, rotation) = AirshipBoardingPose.Resolve(_ship, new Vector3(0f, 3f, 2f));

            Assert.That(Vector3.Distance(position, new Vector3(102f, 53f, 0f)), Is.LessThan(1e-3f), "forward is +x at 90° yaw");
            Assert.That(Quaternion.Angle(rotation, _ship.Transform.rotation), Is.LessThan(1e-3f));
        }

        [Test]
        public void WithARespawnPoint_UsesIt()
        {
            var point = new GameObject("Boarding").AddComponent<AirshipRespawnPoint>();
            point.transform.SetParent(_ship.Transform, worldPositionStays: false);
            point.transform.localPosition = new Vector3(0f, -2f, -5f);
            point.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var (position, rotation) = AirshipBoardingPose.Resolve(_ship, new Vector3(0f, 3f, 0f));

            Assert.That(Vector3.Distance(position, point.transform.position), Is.LessThan(1e-3f));
            Assert.That(Quaternion.Angle(rotation, point.transform.rotation), Is.LessThan(1e-3f));
        }
    }
}
