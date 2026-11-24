#nullable enable
using TinCan.Core.Humanoid;
using UnityEngine;

namespace TinCan.Features.CrewBody
{
    /// <summary>
    /// View: the animated crew body, on the root of the body prefab (the model with its Animator is a child). Put under
    /// a humanoid's visual root by <see cref="CrewBodyUseCase"/>, with this transform at the feet. Each frame it measures
    /// where the body is drawn (after the visual smoothing, order -50) against the ground underfoot and drives the
    /// Animator's Speed, Grounded and VerticalSpeed. Cosmetic and local to each peer: nothing is replicated.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class CrewBodyView : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");

        /// <summary>The ground probe starts this far above the feet (inside the player's own capsule, which it cannot hit).</summary>
        private const float ProbeLift = 0.3f;

        /// <summary>How far below the feet ground still counts as underfoot.</summary>
        private const float ProbeReach = 0.25f;

        private const float SmoothSeconds = 0.08f;

        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Animator? _animator;
        private Transform? _owner;
        private float _runSpeed;
        private float _sprintSpeed;

        private Transform? _frame;
        private bool _hasSample;
        private Vector3 _previousInFrame;
        private Vector3 _velocity;
        private float _secondsWithoutGround;

        /// <summary>Velocity in the body's own axes (x right, y up, z forward), relative to the ground underfoot.</summary>
        public Vector3 LocalVelocity { get; private set; }

        public bool IsGrounded { get; private set; } = true;

        public CrewBodyGait Gait { get; private set; }

        /// <summary>Places the body at the feet of <paramref name="owner"/> and gives it the speeds its clips are made for.</summary>
        public void Attach(Transform owner, float feetHeight, float runSpeed, float sprintSpeed)
        {
            _owner = owner;
            _runSpeed = runSpeed;
            _sprintSpeed = sprintSpeed;
            transform.SetLocalPositionAndRotation(new Vector3(0f, feetHeight, 0f), Quaternion.identity);
            _hasSample = false;
        }

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            // The ship underfoot was destroyed: measure in the world from here.
            if (!ReferenceEquals(_frame, null) && _frame == null)
            {
                _frame = null;
                _hasSample = false;
            }

            // Airborne, the body keeps the frame it left (a jump on a moving ship is measured against the ship).
            bool groundBelow = ProbeGround(out var ground);
            if (groundBelow && ground != _frame)
            {
                _frame = ground;
                _hasSample = false;
            }

            Vector3 inFrame = _frame != null ? _frame.InverseTransformPoint(transform.position) : transform.position;
            if (_hasSample)
            {
                Vector3 measured = HumanoidPresentationProcessor.Velocity(_previousInFrame, inFrame, deltaTime);
                Vector3 world = _frame != null ? _frame.TransformVector(measured) : measured;
                _velocity = HumanoidPresentationProcessor.Smooth(_velocity, world, deltaTime, SmoothSeconds);
            }
            _previousInFrame = inFrame;
            _hasSample = true;

            _secondsWithoutGround = groundBelow ? 0f : _secondsWithoutGround + deltaTime;
            IsGrounded = HumanoidPresentationProcessor.IsGrounded(groundBelow, _velocity.y, _secondsWithoutGround);
            LocalVelocity = transform.InverseTransformDirection(_velocity);

            float speed = CrewBodyProcessor.SignedSpeed(LocalVelocity);
            Gait = CrewBodyProcessor.Gait(IsGrounded, speed, _runSpeed, _sprintSpeed);

            if (_animator == null) return;
            _animator.SetFloat(SpeedId, speed);
            _animator.SetBool(GroundedId, IsGrounded);
            _animator.SetFloat(VerticalSpeedId, _velocity.y);
        }

        /// <summary>
        /// Ground just below the feet, skipping the body's own colliders. <paramref name="frame"/> is what it moves with: a
        /// ship's rigidbody, or null for the world.
        /// </summary>
        private bool ProbeGround(out Transform? frame)
        {
            frame = _frame;
            Vector3 origin = transform.position + Vector3.up * ProbeLift;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, ProbeLift + ProbeReach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.distance >= nearest || (_owner != null && hit.transform.IsChildOf(_owner))) continue;
                nearest = hit.distance;
                frame = hit.rigidbody != null ? hit.rigidbody.transform : null;
                found = true;
            }
            return found;
        }
    }
}
