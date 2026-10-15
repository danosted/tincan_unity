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

        [Test]
        public void PlaceHeld_FirstFrame_HangsTheItemsFromTheEyesAlongTheLook()
        {
            var rig = new FirstPersonViewRig(_config);
            var pose = new ViewPose(new Vector3(1f, 2f, 3f), eyeHeight: 0.7f, pitch: 30f, yaw: 45f);

            var placed = rig.PlaceHeld(_bodyObject.transform, pose, deltaTime: 0.02f);

            var (eyes, look) = FirstPersonViewRig.Eyes(pose);
            var expected = HeldItemSway.Hold(eyes, look, 0.7f);
            Assert.That(placed.HasValue, Is.True);
            Assert.That(Vector3.Distance(placed!.Value.Position, expected.Position), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(placed.Value.Rotation, look), Is.LessThan(1e-3f));
        }

        [Test]
        public void PlaceHeld_AfterATurn_TrailsTheLookWithinTheCap()
        {
            _config.HeldSwayMaxAngle = 5f;
            var rig = new FirstPersonViewRig(_config);
            rig.PlaceHeld(_bodyObject.transform, new ViewPose(Vector3.zero, 0.7f, 0f, 0f), 0.02f);

            var placed = rig.PlaceHeld(_bodyObject.transform, new ViewPose(Vector3.zero, 0.7f, 0f, 60f), 0.02f);

            float lag = Quaternion.Angle(placed!.Value.Rotation, Quaternion.Euler(0f, 60f, 0f));
            Assert.That(lag, Is.GreaterThan(0.1f));
            Assert.That(lag, Is.LessThanOrEqualTo(5.01f));
        }

        [Test]
        public void Take_WithoutAHeldItemLayer_AddsNoOverlayCamera()
        {
            _config.HeldItemLayer = string.Empty;
            var camera = _cameraObject.GetComponent<Camera>();

            new FirstPersonViewRig(_config).Take(camera, System.Array.Empty<Renderer>());

            Assert.That(camera.GetComponentsInChildren<Camera>().Length, Is.EqualTo(1));
        }

        [Test]
        public void Overlay_DrawsOnlyHeldItems_AndDetachPutsEverythingBack()
        {
            const int layer = 30;
            var camera = _cameraObject.GetComponent<Camera>();
            camera.cullingMask = ~0;
            var socket = new GameObject("HeldItems").transform;
            socket.SetParent(_bodyObject.transform, false);
            var item = new GameObject("Carry_Test", typeof(MeshRenderer));
            item.transform.SetParent(socket, false);
            var overlay = new HeldItemOverlay(layer);

            overlay.Attach(camera);
            overlay.Mark(socket);

            Assert.That(camera.cullingMask & (1 << layer), Is.Zero);
            Assert.That(overlay.Overlay, Is.Not.Null);
            Assert.That(overlay.Overlay!.cullingMask, Is.EqualTo(1 << layer));
            Assert.That(item.layer, Is.EqualTo(layer));
            Assert.That(_bodyObject.layer, Is.EqualTo(0), "only what the socket carries moves");

            overlay.Detach();

            Assert.That(camera.cullingMask, Is.EqualTo(~0));
            Assert.That(item.layer, Is.EqualTo(0));
            Assert.That(camera.GetComponentsInChildren<Camera>().Length, Is.EqualTo(1));
        }
    }
}
