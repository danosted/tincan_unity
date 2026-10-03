#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Look;
using TinCan.Features.ThirdPersonCamera;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ThirdPersonViewRig"/>: the orbit behind the drawn body, and the aim height it gives targeting.</summary>
    public class ThirdPersonViewRigTests
    {
        [Test]
        public void Orbit_LevelLook_SitsBehindTheCentre()
        {
            var pose = new ViewPose(new Vector3(1f, 2f, 3f), eyeHeight: 0.7f, pitch: 0f, yaw: 0f);

            var (position, rotation) = ThirdPersonViewRig.Orbit(pose, distance: 5f, height: 1f);

            Assert.That(Vector3.Distance(position, new Vector3(1f, 3f, -2f)), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(rotation, Quaternion.identity), Is.LessThan(1e-3f));
        }

        [Test]
        public void Orbit_LookingDown_RisesBehind()
        {
            var pose = new ViewPose(Vector3.zero, eyeHeight: 0.7f, pitch: 90f, yaw: 0f);

            var (position, _) = ThirdPersonViewRig.Orbit(pose, distance: 5f, height: 0f);

            Assert.That(Vector3.Distance(position, new Vector3(0f, 5f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void Orbit_Yawed_FollowsTheYaw()
        {
            var pose = new ViewPose(Vector3.zero, eyeHeight: 0.7f, pitch: 0f, yaw: 90f);

            var (position, _) = ThirdPersonViewRig.Orbit(pose, distance: 5f, height: 0f);

            Assert.That(Vector3.Distance(position, new Vector3(-5f, 0f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void AimHeight_IsTheOrbitHeight_NotTheEyes()
        {
            var config = ScriptableObject.CreateInstance<ThirdPersonCameraConfig>();
            config.Height = 1.2f;
            try
            {
                Assert.That(new ThirdPersonViewRig(config).AimHeight(eyeHeight: 0.7f), Is.EqualTo(1.2f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
