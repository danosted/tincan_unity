#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TinCan.Features.FirstPersonCamera
{
    /// <summary>
    /// Draws the local player's held items on top of the world so they never sink into a wall or rail: their renderers
    /// move to their own layer, the player's camera stops drawing that layer, and an overlay camera stacked on it
    /// (same lens, no shadows or post-processing of its own: URP post-processes the whole stack once) draws only it.
    /// Detaching puts every layer and the camera back.
    /// </summary>
    public sealed class HeldItemOverlay
    {
        private const string CameraName = "HeldItemCamera";
        private const float FarClip = 10f;

        private readonly int _layer;
        private readonly List<Renderer> _found = new();
        private readonly Dictionary<Renderer, int> _moved = new();
        private Camera? _camera;
        private Camera? _overlay;
        private int _savedCullingMask;

        public HeldItemOverlay(int layer) => _layer = layer;

        public Camera? Overlay => _overlay;

        public void Attach(Camera camera)
        {
            if (_camera != null) Detach();

            _camera = camera;
            _savedCullingMask = camera.cullingMask;
            camera.cullingMask &= ~(1 << _layer);

            var cameraObject = new GameObject(CameraName);
            cameraObject.transform.SetParent(camera.transform, false);
            _overlay = cameraObject.AddComponent<Camera>();
            _overlay.cullingMask = 1 << _layer;
            _overlay.fieldOfView = camera.fieldOfView;
            _overlay.nearClipPlane = camera.nearClipPlane;
            _overlay.farClipPlane = FarClip;

            var overlayData = _overlay.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderShadows = false;
            overlayData.renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().cameraStack?.Add(_overlay);
        }

        /// <summary>Moves renderers under <paramref name="held"/> that are not on the layer yet (items equipped since).</summary>
        public void Mark(Transform held)
        {
            if (_camera == null) return;

            held.GetComponentsInChildren(true, _found);
            foreach (var renderer in _found)
            {
                if (_moved.ContainsKey(renderer)) continue;
                _moved[renderer] = renderer.gameObject.layer;
                renderer.gameObject.layer = _layer;
            }
        }

        public void Detach()
        {
            foreach (var (renderer, layer) in _moved)
            {
                if (renderer != null) renderer.gameObject.layer = layer;
            }
            _moved.Clear();

            if (_camera != null)
            {
                _camera.cullingMask = _savedCullingMask;
                if (_overlay != null) _camera.GetUniversalAdditionalCameraData().cameraStack?.Remove(_overlay);
            }

            if (_overlay != null)
            {
                if (Application.isPlaying) Object.Destroy(_overlay.gameObject);
                else Object.DestroyImmediate(_overlay.gameObject);
            }

            _camera = null;
            _overlay = null;
        }
    }
}
