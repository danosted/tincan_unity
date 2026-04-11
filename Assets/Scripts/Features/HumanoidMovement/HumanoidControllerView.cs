using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Features.Possession;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// View/Infrastructure Layer: Unity-specific implementation using CharacterController.
    /// Handles physical movement, ground detection, and actor lifecycle.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HumanoidControllerView : ControllableActorBase, IHumanoidMovementView, IPossessionResponder
    {
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 7f;
        [SerializeField] private float _sprintMultiplier = 1.8f;
        [SerializeField] private float _jumpForce = 8f;
        [SerializeField] private float _gravity = 20f;

        private CharacterController _controller;
        private GroundData _currentGround;
        private IMovingPlatform _activePlatform;


        public Transform Transform => transform;
        public GroundData CurrentGround => _currentGround;
        public bool IsGrounded => _controller.isGrounded;
        public float WalkSpeed => _walkSpeed;
        public float SprintMultiplier => _sprintMultiplier;
        public float JumpForce => _jumpForce;
        public float Gravity => _gravity;

        public Quaternion LookRotation
        {
            get
            {
                var lookView = GetComponent<IHumanoidLookView>();
                return lookView != null ? Quaternion.Euler(0, lookView.Yaw, 0) : transform.rotation;
            }
        }

        protected void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            UpdateGroundData();
        }

        private void UpdateGroundData()
        {
            _currentGround.IsGrounded = _controller.isGrounded;

            if (_activePlatform != null)
            {
                _currentGround.SurfaceDelta = _activePlatform.PositionDelta;
                _currentGround.GroundVelocity = _activePlatform.Velocity;
            }
            else
            {
                _currentGround.SurfaceDelta = Vector3.zero;
                _currentGround.GroundVelocity = Vector3.zero;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Detect if we hit a moving platform
            var platform = hit.gameObject.GetComponentInParent<IMovingPlatform>();
            _activePlatform = platform;

            if (hit.normal.y > 0.7f) // Valid ground normal
            {
                _currentGround.GroundNormal = hit.normal;
            }
        }

        public void Move(Vector3 motion)
        {
            _controller.Move(motion);
        }

        public void SetRotation(Quaternion rotation)
        {
            transform.rotation = rotation;
        }

        public void OnPossessed()
        {
            EnableControls();
        }

        public void OnUnpossessed()
        {
            DisableControls();
        }
    }
}
