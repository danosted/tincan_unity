#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Look;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinCan.Features.FirstPersonCamera
{
    /// <summary>
    /// The first-person rig: the camera sits at the eyes of where the body is drawn and looks along the look. While the
    /// local player looks through it, the camera takes the configured field of view and near clip, and their own body
    /// draws only its shadow. Releasing restores both. Targeting's CameraAim then looks from the eyes, as EyeAim does.
    /// </summary>
    public sealed class FirstPersonViewRig : IViewRig
    {
        private readonly FirstPersonCameraConfig _config;
        private float _savedFieldOfView;
        private float _savedNearClip;
        private readonly List<(Renderer Renderer, ShadowCastingMode Mode)> _hidden = new();

        public FirstPersonViewRig(FirstPersonCameraConfig config) => _config = config;

        public void Take(Camera camera, IReadOnlyList<Renderer> body)
        {
            _savedFieldOfView = camera.fieldOfView;
            _savedNearClip = camera.nearClipPlane;
            camera.fieldOfView = _config.FieldOfView;
            camera.nearClipPlane = _config.NearClip;

            _hidden.Clear();
            if (!_config.HideBody) return;
            foreach (var renderer in body)
            {
                if (renderer == null) continue;
                _hidden.Add((renderer, renderer.shadowCastingMode));
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        public void Release(Camera camera, IReadOnlyList<Renderer> body)
        {
            camera.fieldOfView = _savedFieldOfView;
            camera.nearClipPlane = _savedNearClip;

            foreach (var (renderer, mode) in _hidden)
            {
                if (renderer != null) renderer.shadowCastingMode = mode;
            }
            _hidden.Clear();
        }

        public (Vector3 Position, Quaternion Rotation) Place(in ViewPose pose) => Eyes(pose);

        public float AimHeight(float eyeHeight) => eyeHeight;

        public static (Vector3 Position, Quaternion Rotation) Eyes(in ViewPose pose) =>
            (pose.VisualPosition + Vector3.up * pose.EyeHeight, Quaternion.Euler(pose.Pitch, pose.Yaw, 0f));
    }
}
