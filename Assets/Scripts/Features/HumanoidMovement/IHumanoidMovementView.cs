using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: Interface for the physical movement motor.
    /// Purely behavioral, delegated to by a Character Facade or Mediator.
    /// </summary>
    public interface IHumanoidMovementView : IControllable
    {
        Transform Transform { get; }
        GroundData CurrentGround { get; }
        void UpdateGroundData(GroundData data);

        float WalkSpeed { get; }
        float SprintMultiplier { get; }
        float JumpForce { get; }
        float Gravity { get; }

        /// <summary>
        /// The direction the camera is currently looking (used for movement relativity).
        /// </summary>
        Quaternion LookRotation { get; }

        /// <summary>Aim pitch in degrees from the look view (positive looks down); 0 when there is none.</summary>
        float LookPitch { get; }

        /// <summary>
        /// Height of the eyes above the body root, along the body's up. The root is the capsule's centre, not the feet,
        /// so this is small (about 0.7 for a 2 m capsule). Targeting aims from here.
        /// </summary>
        float EyeHeight { get; }

        /// <summary>
        /// Raw sensing data from the physical world.
        /// </summary>
        RaycastHit? LastGroundHit { get; }

        /// <summary>
        /// Samples the physical world before one movement simulation tick.
        /// </summary>
        void RefreshSensing();

        void Move(Vector3 motion);

        /// <summary>
        /// Moves with the platform underneath between simulation ticks, without collision and without changing the
        /// grounded state the last simulated move produced.
        /// </summary>
        void Carry(Vector3 displacement);

        /// <summary>A simulation tick finished: the pose is the new target for drawing (visual interpolation).</summary>
        void CommitSimulatedPose();

        /// <summary>Prediction moved the body by <paramref name="worldDelta"/> outside a tick; draw it as a fade, not a pop.</summary>
        void AbsorbCorrection(Vector3 worldDelta);

        /// <summary>
        /// Sets the physical rotation of the character body.
        /// </summary>
        void SetRotation(Quaternion rotation);

        /// <summary>
        /// Applies an authoritative pose before replaying predicted inputs.
        /// </summary>
        void SetPose(Vector3 position, Quaternion rotation);
    }
}
