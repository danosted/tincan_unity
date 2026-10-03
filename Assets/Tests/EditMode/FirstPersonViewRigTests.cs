#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Look;
using TinCan.Features.FirstPersonCamera;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="FirstPersonViewRig"/>: the camera at the drawn eyes; the body only casts its shadow while taken.</summary>
    public class FirstPersonViewRigTests
    {
        private FirstPersonCameraConfig _config = null!;
        private GameObject _cameraObject = null!;
        private GameObject _bodyObject = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<FirstPersonCameraConfig>();
            _config.FieldOfView = 80f;
            _config.NearClip = 0.05f;
            _cameraObject = new GameObject("Camera", typeof(Camera));
            _bodyObject = new GameObject("Visual", typeof(MeshRenderer));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_cameraObject);
            Object.DestroyImmediate(_bodyObject);
        }

        [Test]
        public void Eyes_SitAtEyeHeightAboveTheDrawnBody_AlongTheLook()
        {
            var pose = new ViewPose(new Vector3(1f, 2f, 3f), eyeHeight: 0.7f, pitch: 10f, yaw: 30f);

            var (position, rotation) = FirstPersonViewRig.Eyes(pose);

            Assert.That(Vector3.Distance(position, new Vector3(1f, 2.7f, 3f)), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(rotation, Quaternion.Euler(10f, 30f, 0f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void AimHeight_IsTheEyes()
        {
            Assert.That(new FirstPersonViewRig(_config).AimHeight(eyeHeight: 0.7f), Is.EqualTo(0.7f));
        }

        [Test]
        public void TakeAndRelease_SetAndRestoreTheLensAndTheBody()
        {
            var rig = new FirstPersonViewRig(_config);
            var camera = _cameraObject.GetComponent<Camera>();
            var body = _bodyObject.GetComponent<MeshRenderer>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            body.shadowCastingMode = ShadowCastingMode.On;

            rig.Take(camera, new Renderer[] { body });

            Assert.That(camera.fieldOfView, Is.EqualTo(80f).Within(1e-3f));
            Assert.That(camera.nearClipPlane, Is.EqualTo(0.05f).Within(1e-5f));
            Assert.That(body.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));

            rig.Release(camera, new Renderer[] { body });

            Assert.That(camera.fieldOfView, Is.EqualTo(60f).Within(1e-3f));
            Assert.That(camera.nearClipPlane, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(body.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void Take_WithHideBodyOff_LeavesTheBodyDrawn()
        {
            _config.HideBody = false;
            var body = _bodyObject.GetComponent<MeshRenderer>();
            body.shadowCastingMode = ShadowCastingMode.On;

            new FirstPersonViewRig(_config).Take(_cameraObject.GetComponent<Camera>(), new Renderer[] { body });

            Assert.That(body.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }
    }
}
