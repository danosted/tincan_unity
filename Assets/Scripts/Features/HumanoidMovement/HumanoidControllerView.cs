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
    public class HumanoidControllerView : ControllableActorBase, IHumanoidMovementView, IPossessionReceiver
    {
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 7f;
        [SerializeField] private float _sprintMultiplier = 1.8f;
        [SerializeField] private float _jumpForce = 8f;
        [SerializeField] private float _gravity = 20f;

        private CharacterController _controller;
        private GroundData _currentGround;
        private IMovingGround _activeMovingGround;

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

            // Use a raycast to verify the ground and platform
            bool hasPlatform = false;
            if (_controller.isGrounded)
            {
                RaycastHit hit;
                // Raycast slightly further than the controller's skin width
                if (Physics.Raycast(transform.position, Vector3.down, out hit, _controller.height * 0.5f + 0.2f))
                {
                    var platform = hit.collider.gameObject.GetComponentInParent<IMovingGround>();
                    _currentGround.GroundTransform = hit.transform; // Always track what we are standing on

                    if (platform != null)
                    {
                        _activeMovingGround = platform;
                        _currentGround.GroundNormal = hit.normal;
                        hasPlatform = true;
                    }
                }
            }

            if (hasPlatform && _activeMovingGround != null)
            {
                // Use velocity for smooth, frame-rate independent movement
                _currentGround.GroundVelocity = _activeMovingGround.Velocity;
                _currentGround.SurfaceDelta = _activeMovingGround.PositionDelta;
                _currentGround.RotationDelta = _activeMovingGround.RotationDelta;
            }
            else
            {
                _activeMovingGround = null;
                _currentGround.GroundTransform = null;
                _currentGround.GroundVelocity = Vector3.zero;
                _currentGround.SurfaceDelta = Vector3.zero;
                _currentGround.RotationDelta = Quaternion.identity;
                _currentGround.GroundNormal = Vector3.up;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Fallback detection if raycast missed but we hit something while moving
            if (!_currentGround.IsGrounded) return;

            var platform = hit.gameObject.GetComponentInParent<IMovingGround>();
            if (platform != null)
            {
                _activeMovingGround = platform;
            }

            if (hit.normal.y > 0.7f)
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

        public void OnPossessed(ulong playerId)
        {
            EnableControls();
        }

        public void OnUnpossessed()
        {
            DisableControls();
        }
    }
}
