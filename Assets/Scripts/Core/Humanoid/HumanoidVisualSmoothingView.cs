#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Core.Humanoid
{
    /// <summary>Where a humanoid is drawn, which can trail its simulated root. The camera and telemetry follow this.</summary>
    public interface IHumanoidVisualAnchor
    {
        Vector3 VisualPosition { get; }

        /// <summary>The body's own renderers (on the visual root itself, not what it holds), for rigs that hide the body.</summary>
        IReadOnlyList<Renderer> BodyRenderers { get; }

        /// <summary>
        /// Carries the held items (the equipment mediator puts their visuals here). At rest it sits on the visual root;
        /// a camera rig may move it for the local player (first person keeps it in view). Null without a visual root.
        /// </summary>
        Transform? HeldSocket { get; }
    }

    /// <summary>
    /// View: draws the humanoid's visuals (the <see cref="_visualRoot"/> child: mesh, carried items) at the
    /// interpolated pose from <see cref="HumanoidVisualInterpolation"/> while the root, with its CharacterController,
    /// stays at the simulated pose. Runs after the platform carry (HumanoidPlayer, order -100) and before the camera
    /// (LookView, order 0). Humanoids not simulated here (proxies) are drawn at the root.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class HumanoidVisualSmoothingView : MonoBehaviour, IHumanoidVisualAnchor
    {
        [SerializeField] private Transform? _visualRoot;

        private readonly HumanoidVisualInterpolation _interpolation = new();

        public Vector3 VisualPosition => _visualRoot != null ? _visualRoot.position : transform.position;

        public IReadOnlyList<Renderer> BodyRenderers => _visualRoot != null ? _visualRoot.GetComponents<Renderer>() : System.Array.Empty<Renderer>();

        /// <summary>The held-items socket's name; the equipment mediator finds it by this name.</summary>
        public const string HeldSocketName = "HeldItems";

        public Transform? HeldSocket { get; private set; }

        private void Awake()
        {
            if (_visualRoot == null) return;

            HeldSocket = _visualRoot.Find(HeldSocketName);
            if (HeldSocket != null) return;
            HeldSocket = new GameObject(HeldSocketName).transform;
            HeldSocket.SetParent(_visualRoot, false);
        }

        public void Commit(Transform? platform) => _interpolation.Commit(platform, transform.position, transform.rotation, Time.time);

        public void AbsorbCorrection(Vector3 worldDelta) => _interpolation.AbsorbCorrection(worldDelta);

        private void LateUpdate()
        {
            if (_visualRoot == null) return;

            if (!_interpolation.IsActive(Time.time))
            {
                _visualRoot.localPosition = Vector3.zero;
                _visualRoot.localRotation = Quaternion.identity;
                return;
            }

            _interpolation.Evaluate(Time.time, Time.deltaTime, out var position, out var rotation);
            _visualRoot.SetPositionAndRotation(position, rotation);
        }

        private void OnDisable() => _interpolation.Reset();
    }
}
