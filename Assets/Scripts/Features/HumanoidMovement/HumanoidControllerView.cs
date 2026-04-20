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
    public class HumanoidControllerView : MonoBehaviour, IControllable, IHumanoidMovementView, IPossessionReceiver
    {
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 7f;
        [SerializeField] private float _sprintMultiplier = 1.8f;
        [SerializeField] private float _jumpForce = 8f;
        [SerializeField] private float _gravity = 20f;
        [SerializeField] private LayerMask _interactableMask = ~0; // Default: hit everything

        private CharacterController _controller;
        private GroundData _currentGround;
        private RaycastHit? _lastGroundHit;

        public bool IsControlsEnabled { get; private set; } = false;

        public void DisableControls()
        {
            IsControlsEnabled = false;
        }

        public void EnableControls()
        {
            IsControlsEnabled = true;
        }

        public Transform Transform => transform;
        public GroundData CurrentGround => _currentGround;
        public RaycastHit? LastGroundHit => _lastGroundHit;
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
            UpdateSensing();
        }

        private void UpdateSensing()
        {
            // Raw sensing: Perform a raycast regardless of isGrounded state to detect platforms
            // Increased range to 2.0m to maintain stickiness during jumps
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out var hit, _controller.height * 0.5f + 2.0f, _interactableMask))
            {
                _lastGroundHit = hit;
                Debug.DrawLine(transform.position, hit.point, Color.green);
            }
            else
            {
                _lastGroundHit = null;
            }

            // Infrastructure state
            _currentGround.IsGrounded = _controller.isGrounded;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Fallback for normal detection
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

        public void UpdateGroundData(GroundData data)
        {
            _currentGround = data;
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
