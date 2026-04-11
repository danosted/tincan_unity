using System;
using UnityEngine;
using TinCan.Features.Possession;
using TinCan.Core.Domain;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// View/Infrastructure Layer: Orbital third-person camera implementation.
    /// Attach this to the character prefab.
    /// </summary>
    [RequireComponent(typeof(HumanoidControllerView))]
    public class ThirdPersonLookView : MonoBehaviour, IHumanoidLookView, IPossessionResponder
    {
        [Header("Camera Configuration")]
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private float _distance = 5f;
        [SerializeField] private float _sensitivity = 0.5f;
        [SerializeField] private float _maxPitch = 85f;

        public bool IsActive { get; private set; } = false;
        public float Pitch { get; set; }
        public float Yaw { get; set; }
        public float Sensitivity => _sensitivity;
        public float MaxPitch => _maxPitch;

        private void Start()
        {
            // Initialize from current rotation if pivot exists
            if (_cameraPivot != null)
            {
                Vector3 euler = _cameraPivot.eulerAngles;
                Yaw = euler.y;
                Pitch = euler.x;
                if (Pitch > 180) Pitch -= 360;
            }
        }

        private void LateUpdate()
        {
            if (!IsActive || _cameraPivot == null) return;

            // Simple orbital camera math
            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0);
            Vector3 position = transform.position - (rotation * Vector3.forward * _distance);

            _cameraPivot.rotation = rotation;
            _cameraPivot.position = position;
        }

        public void ApplyLook(float pitch, float yaw)
        {
            Pitch = pitch;
            Yaw = yaw;
        }

        public void OnPossessed()
        {
            IsActive = true;
            _cameraPivot.gameObject.SetActive(true);
        }
        public void OnUnpossessed()
        {
            IsActive = false;
            _cameraPivot.gameObject.SetActive(false);
        }
    }
}
