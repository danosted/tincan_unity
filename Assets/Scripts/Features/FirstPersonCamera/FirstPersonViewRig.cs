#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Look;
using TinCan.Core.Domain.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace TinCan.Features.FirstPersonCamera
{
    /// <summary>
    /// The first-person rig: the camera sits at the eyes of where the body is drawn and looks along the look. While the
    /// local player looks through it, the camera takes the configured field of view and near clip, and their own body
    /// draws only its shadow. Their held items follow the look, trailing it slightly (<see cref="HeldItemSway"/>), and
    /// are drawn on top of the world (<see cref="HeldItemOverlay"/>). Releasing restores all of it. Targeting's
    /// CameraAim then looks from the eyes, as EyeAim does.
    /// </summary>
    public sealed class FirstPersonViewRig : IViewRig
    {
        /// <summary>The player's field of view; contributed by this feature's installer, its default the config's.</summary>
        public const string FieldOfViewKey = "view.fov";

        private readonly FirstPersonCameraConfig _config;
        private readonly IPlayerSettings? _settings;
        private float _savedFieldOfView;
        private float _savedNearClip;
        private readonly List<(Renderer Renderer, ShadowCastingMode Mode)> _hidden = new();
        private HeldItemOverlay? _overlay;
        private Quaternion? _trailing;
        private Camera? _held;

        public FirstPersonViewRig(FirstPersonCameraConfig config) : this(config, null) { }

        /// <summary>With the player's settings: the field of view is theirs, and a change shows at once.</summary>
        [Inject]
        public FirstPersonViewRig(FirstPersonCameraConfig config, IPlayerSettings? settings)
        {
            _config = config;
            _settings = settings;
            if (_settings != null) _settings.Changed += OnSettingChanged;
        }

        /// <summary>The field of view to look through: the player's setting where there is one, else the config's.</summary>
        public float FieldOfView => _settings != null && _settings.Has(FieldOfViewKey) ? _settings.GetFloat(FieldOfViewKey) : _config.FieldOfView;

        public void Take(Camera camera, IReadOnlyList<Renderer> body)
        {
            _held = camera;
            _savedFieldOfView = camera.fieldOfView;
            _savedNearClip = camera.nearClipPlane;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = _config.NearClip;
            _trailing = null;
            AttachOverlay(camera);

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
            _held = null;
            camera.fieldOfView = _savedFieldOfView;
            camera.nearClipPlane = _savedNearClip;
            _overlay?.Detach();
            _overlay = null;

            foreach (var (renderer, mode) in _hidden)
            {
                if (renderer != null) renderer.shadowCastingMode = mode;
            }
            _hidden.Clear();
        }

        public (Vector3 Position, Quaternion Rotation) Place(in ViewPose pose) => Eyes(pose);

        public float AimHeight(float eyeHeight) => eyeHeight;

        public (Vector3 Position, Quaternion Rotation)? PlaceHeld(Transform held, in ViewPose pose, float deltaTime)
        {
            _overlay?.Mark(held);

            var (eyes, look) = Eyes(pose);
            var trailing = _trailing.HasValue
                ? HeldItemSway.Trail(_trailing.Value, look, _config.HeldSwaySharpness, _config.HeldSwayMaxAngle, deltaTime)
                : look;
            _trailing = trailing;
            return HeldItemSway.Hold(eyes, trailing, pose.EyeHeight);
        }

        public static (Vector3 Position, Quaternion Rotation) Eyes(in ViewPose pose) =>
            (pose.VisualPosition + Vector3.up * pose.EyeHeight, Quaternion.Euler(pose.Pitch, pose.Yaw, 0f));

        private void OnSettingChanged(string key)
        {
            if (key == FieldOfViewKey && _held != null) _held.fieldOfView = FieldOfView;
        }

        private void AttachOverlay(Camera camera)
        {
            _overlay?.Detach();
            _overlay = null;
            if (string.IsNullOrEmpty(_config.HeldItemLayer)) return;

            int layer = LayerMask.NameToLayer(_config.HeldItemLayer);
            if (layer < 0)
            {
                Debug.LogWarning($"[{nameof(FirstPersonViewRig)}] Layer '{_config.HeldItemLayer}' is missing from Tags and Layers; held items are drawn with the world and can clip into walls.");
                return;
            }

            _overlay = new HeldItemOverlay(layer);
            _overlay.Attach(camera);
        }
    }
}
