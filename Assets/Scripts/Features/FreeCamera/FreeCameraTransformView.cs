using System;
using TinCan.Features.Possession;
using UnityEngine;
using VContainer;

namespace TinCan.Features.FreeCamera
{
    /// <summary>
    /// View/Infrastructure Layer: Unity-specific implementation of the Free Camera view.
    /// This component bridges the pure logic to the Unity Transform.
    /// </summary>
    [RequireComponent(typeof(PossessionCameraResponder))]
    public class FreeCameraTransformView : MonoBehaviour, IControllable, IFreeCameraView
    {
        [SerializeField] private float _moveSpeed = 10f;
        [SerializeField] private float _rotationSensitivity = 0.5f;
        [SerializeField] private float _maxPitchAngle = 90f;
        [SerializeField] private bool _lockCursorOnStart = true;

        public bool IsControlsEnabled { get; private set; } = false;

        public void DisableControls()
        {
            IsControlsEnabled = false;
        }

        public void EnableControls()
        {
            IsControlsEnabled = true;
        }

        public Transform CameraTransform => transform;
        public bool IsActive => IsControlsEnabled;
        public float MoveSpeed => _moveSpeed;
        public float RotationSensitivity => _rotationSensitivity;
        public float MaxPitchAngle => _maxPitchAngle;
        public bool ShouldLockCursor => _lockCursorOnStart;

        public float CurrentPitch { get; set; }
        public float CurrentYaw { get; set; }

        public Guid Id { get; } = Guid.NewGuid();

        public bool IsSimulating => IsActive;
        public ulong? PossessorId { get; private set; }

        private void Start()
        {
            // Initialize rotation state from current transform
            Vector3 euler = transform.eulerAngles;
            CurrentYaw = euler.y;
            CurrentPitch = euler.x;
            if (CurrentPitch > 180) CurrentPitch -= 360;
        }

        public void ApplyRotation(float pitch, float yaw)
        {
            CurrentPitch = pitch;
            CurrentYaw = yaw;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        public void OnPossessed(ulong playerId)
        {
            EnableControls();
        }

        public void OnUnpossessed()
        {
            DisableControls();
        }

        public bool CanPossess(ulong playerId)
        {
            return PossessorId == null || PossessorId == playerId;
        }

        public void AuthoritativeSetPossessor(ulong? playerId)
        {
            PossessorId = playerId;
        }
    }
}
