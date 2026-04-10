// using UnityEngine;

// public class AirshipController : ControllableActorBase, IControllableAirship
// {
//     // Movement Parameters
//     [Header("Movement Settings")]
//     [SerializeField] private float maxForwardSpeed = 10f; // Max velocity for 'X' forward
//     [SerializeField] private float maxBackwardSpeed = 5f; // Max velocity for 'X' backward
//     [SerializeField] private float accelerationRate = 2f; // How fast to reach max speed
//     [SerializeField] private float decelerationRate = 5f; // How fast to slow down when no input
//     [SerializeField] private float turnSpeed = 90f; // Yaw rotation speed for A/D

//     [Header("Rigidbody Settings")]
//     [SerializeField] private float rbDrag = 1f;
//     [SerializeField] private float rbAngularDrag = 0.05f;

//     private Rigidbody rb;
//     private float currentThrottleInput; // -1 (backward) to 1 (forward)
//     private float currentTurnInput;     // -1 (left) to 1 (right)
//     private float currentPitchInput;    // -1 (down) to 1 (up)

//     // Current speeds/rotations
//     private float currentSpeed = 0f;

//     protected override void Awake()
//     {
//         base.Awake();
//         rb = GetComponent<Rigidbody>();
//         if (rb == null)
//         {
//             Debug.LogError("AirshipController requires a Rigidbody component on the GameObject.");
//             enabled = false; // Disable the script if no Rigidbody is found
//         }

//         // Apply Rigidbody settings
//         rb.linearDamping = rbDrag;
//         rb.angularDamping = rbAngularDrag;
//     }

//     void Update()
//     {
//         // Input is received via IControllable methods from a PlayerController
//     }

//     void FixedUpdate()
//     {
//         if (IsControlsEnabled)
//         {
//             ApplyMovement();
//         }
//     }

//     // IControllable Input Methods
//     public void SetThrottleInput(float throttle)
//     {
//         currentThrottleInput = Mathf.Clamp(throttle, -1f, 1f);
//     }

//     public void SetTurnInput(float turn)
//     {
//         currentTurnInput = Mathf.Clamp(turn, -1f, 1f);
//     }

//     public void SetPitchInput(float pitch)
//     {
//         currentPitchInput = Mathf.Clamp(pitch, -1f, 1f);
//     }

//     private void ApplyMovement()
//     {
//         // 1. Calculate desired speed based on throttle input and acceleration/deceleration
//         float targetSpeed = 0f;
//         if (currentThrottleInput > 0)
//         {
//             targetSpeed = maxForwardSpeed * currentThrottleInput;
//         }
//         else if (currentThrottleInput < 0)
//         {
//             targetSpeed = maxBackwardSpeed * currentThrottleInput;
//         }

//         // Gradually adjust currentSpeed
//         if (currentThrottleInput != 0)
//         {
//             currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelerationRate * Time.fixedDeltaTime);
//         }
//         else // Decelerate when no throttle input
//         {
//             currentSpeed = Mathf.MoveTowards(currentSpeed, 0, decelerationRate * Time.fixedDeltaTime);
//         }

//         // 2. Apply forward/backward force
//         // Using ForceMode.VelocityChange for direct velocity manipulation, or ForceMode.Force for continuous acceleration
//         // For a more 'building up' feel, ForceMode.Force combined with drag might be better.
//         // Let's use ForceMode.VelocityChange for direct control towards currentSpeed.
//         Vector3 targetVelocity = transform.forward * currentSpeed;
//         Vector3 velocityChange = targetVelocity - rb.linearVelocity;
//         rb.AddForce(velocityChange, ForceMode.VelocityChange);


//         // 3. Apply Rotation (Yaw and Pitch)
//         // Yaw from A/D and Mouse X
//         float yawRotation = currentTurnInput * turnSpeed * Time.fixedDeltaTime;
//         Quaternion yawDelta = Quaternion.Euler(0, yawRotation, 0);

//         // Pitch from Mouse Y
//         float pitchRotation = currentPitchInput * turnSpeed * Time.fixedDeltaTime; // Reusing turnSpeed for simplicity
//         Quaternion pitchDelta = Quaternion.Euler(pitchRotation, 0, 0);

//         // Combine rotations
//         Quaternion deltaRotation = yawDelta * pitchDelta;
//         rb.MoveRotation(rb.rotation * deltaRotation);
//     }
// }
