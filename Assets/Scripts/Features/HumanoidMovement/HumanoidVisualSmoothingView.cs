#nullable enable
using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>Where a humanoid is drawn, which can trail its simulated root. The camera and telemetry follow this.</summary>
    public interface IHumanoidVisualAnchor
    {
        Vector3 VisualPosition { get; }
    }

    /// <summary>
    /// View: draws the humanoid's visuals (the <see cref="_visualRoot"/> child: mesh, carried items) at the
    /// interpolated pose from <see cref="HumanoidVisualInterpolation"/> while the root, with its CharacterController,
    /// stays at the simulated pose. Runs after the platform carry (HumanoidPlayer, order -100) and before the camera
    /// (ThirdPersonLookView, order 0). Humanoids not simulated here (proxies) are drawn at the root.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class HumanoidVisualSmoothingView : MonoBehaviour, IHumanoidVisualAnchor
    {
        [SerializeField] private Transform? _visualRoot;

        private readonly HumanoidVisualInterpolation _interpolation = new();

        public Vector3 VisualPosition => _visualRoot != null ? _visualRoot.position : transform.position;

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
