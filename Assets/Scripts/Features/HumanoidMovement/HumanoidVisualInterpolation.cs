#nullable enable
using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Where to draw a humanoid between simulation ticks. Simulation moves the body at the tick rate (30 Hz); drawing
    /// it there makes it step on a high-refresh display. This keeps the last two simulated poses in the local space of
    /// the platform underfoot and blends between them by the time elapsed since the last tick, re-expressed against
    /// the platform's current pose so the body rides an interpolated ship smoothly. Prediction corrections are
    /// absorbed as an offset that fades out instead of popping. The drawn pose trails the simulation by up to one tick.
    /// </summary>
    public sealed class HumanoidVisualInterpolation
    {
        public const float CorrectionFadeTime = 0.1f;
        public const float TeleportDistance = 3f;
        public const float MinTickInterval = 1f / 120f;
        public const float MaxTickInterval = 0.2f;
        public const float StaleAfter = 0.25f;

        private Transform? _platform;
        private Vector3 _previousPosition;
        private Vector3 _currentPosition;
        private Quaternion _previousRotation = Quaternion.identity;
        private Quaternion _currentRotation = Quaternion.identity;
        private Vector3 _offset;
        private float _lastCommitTime = float.NegativeInfinity;
        private float _tickInterval = 1f / 30f;
        private bool _hasPose;

        /// <summary>True while ticks keep arriving; a humanoid that is not simulated here (a proxy) is drawn as is.</summary>
        public bool IsActive(float time) => _hasPose && time - _lastCommitTime <= StaleAfter;

        public Vector3 Offset => _offset;

        /// <summary>Records the simulated pose after a tick.</summary>
        public void Commit(Transform? platform, Vector3 worldPosition, Quaternion worldRotation, float time)
        {
            ToLocal(platform, worldPosition, worldRotation, out var position, out var rotation);

            if (!_hasPose || platform != _platform || Vector3.Distance(position, _currentPosition) >= TeleportDistance)
            {
                // First pose, a new frame of reference or a teleport: nothing sensible to blend from.
                _previousPosition = _currentPosition = position;
                _previousRotation = _currentRotation = rotation;
                _offset = Vector3.zero;
            }
            else
            {
                _previousPosition = _currentPosition;
                _previousRotation = _currentRotation;
                _currentPosition = position;
                _currentRotation = rotation;
            }

            if (_hasPose && time > _lastCommitTime)
            {
                _tickInterval = Mathf.Clamp(time - _lastCommitTime, MinTickInterval, MaxTickInterval);
            }

            _platform = platform;
            _lastCommitTime = time;
            _hasPose = true;
        }

        /// <summary>
        /// The simulated body just jumped by <paramref name="worldDelta"/> outside a normal tick (prediction replay).
        /// The recorded trajectory is moved with it and the jump is shown as a fading offset; teleport-sized jumps
        /// are shown at once.
        /// </summary>
        public void AbsorbCorrection(Vector3 worldDelta)
        {
            if (!_hasPose) return;

            Vector3 localDelta = _platform != null ? Quaternion.Inverse(_platform.rotation) * worldDelta : worldDelta;
            _previousPosition += localDelta;
            _currentPosition += localDelta;

            if (worldDelta.magnitude >= TeleportDistance)
            {
                _previousPosition = _currentPosition;
                _offset = Vector3.zero;
                return;
            }

            _offset -= localDelta;
        }

        /// <summary>The pose to draw at <paramref name="time"/>. <paramref name="deltaTime"/> fades the correction offset.</summary>
        public void Evaluate(float time, float deltaTime, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            float t = Mathf.Clamp01((time - _lastCommitTime) / _tickInterval);
            _offset *= Mathf.Exp(-Mathf.Max(0f, deltaTime) / CorrectionFadeTime);

            Vector3 position = Vector3.Lerp(_previousPosition, _currentPosition, t) + _offset;
            Quaternion rotation = Quaternion.Slerp(_previousRotation, _currentRotation, t);

            if (_platform != null)
            {
                worldPosition = _platform.position + _platform.rotation * position;
                worldRotation = _platform.rotation * rotation;
                return;
            }

            worldPosition = position;
            worldRotation = rotation;
        }

        public void Reset()
        {
            _hasPose = false;
            _offset = Vector3.zero;
            _platform = null;
        }

        private static void ToLocal(Transform? platform, Vector3 worldPosition, Quaternion worldRotation, out Vector3 position, out Quaternion rotation)
        {
            if (platform == null)
            {
                position = worldPosition;
                rotation = worldRotation;
                return;
            }

            var inverse = Quaternion.Inverse(platform.rotation);
            position = inverse * (worldPosition - platform.position);
            rotation = inverse * worldRotation;
        }
    }
}
