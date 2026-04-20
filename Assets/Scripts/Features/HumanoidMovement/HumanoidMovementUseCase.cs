using UnityEngine;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Possession;
using System.Collections.Generic;
using System;
using System.Linq;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Application Layer: Coordinates input and domain logic to move the humanoid character.
    /// Inherits from SimulationUseCase for unified actor simulation.
    /// </summary>
    public class HumanoidMovementUseCase : SimulationUseCase<IHumanoidCharacterView, HumanoidInputState>
    {
        private readonly HumanoidMovementProcessor _processor;
        private readonly Dictionary<Guid, Vector3> _horizontalVelocities = new();
        private readonly Dictionary<Guid, float> _verticalVelocities = new();

        public HumanoidMovementUseCase(
            IInputService inputService,
            INetworkService networkService,
            HumanoidMovementProcessor processor,
            IActorRegistry registry,
            ITimeService timeService)
            : base(inputService, networkService, registry, timeService)
        {
            _processor = processor;
        }

        protected override HumanoidInputState GatherLocalInput(IHumanoidCharacterView character)
        {
            var movement = character.Movement;
            if (movement.IsControlsEnabled == false) return character.InputState; // Return last known input if controls are disabled

            float horizontal = InputService.GetAxis(ActionNames.MoveRight, ActionNames.MoveLeft);
            float vertical = InputService.GetAxis(ActionNames.MoveForward, ActionNames.MoveBackward);
            Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;
            bool jumpTriggered = InputService.WasActionTriggered(ActionNames.Jump) || InputService.IsActionPressed(ActionNames.Jump);
            bool isSprinting = InputService.IsActionPressed(ActionNames.Sprint);

            return new HumanoidInputState
            {
                MovementDirection = inputDirection,
                IsJumping = jumpTriggered,
                IsSprinting = isSprinting,
                LookRotation = character.Movement.LookRotation
            };
        }

        protected override void ProcessSimulation(IHumanoidCharacterView character, HumanoidInputState input, bool isCaptured)
        {
            var movement = character.Movement;

            // 1. Resolve grounding and platforms
            GroundData ground = ResolveGrounding(character);
            movement.UpdateGroundData(ground);

            float deltaTime = TimeService.DeltaTime;

            // Initialize velocity tracking for this specific actor if missing
            if (!_horizontalVelocities.ContainsKey(character.Id)) _horizontalVelocities[character.Id] = Vector3.zero;
            if (!_verticalVelocities.ContainsKey(character.Id)) _verticalVelocities[character.Id] = 0f;

            // Use the authoritative look rotation (either local or synced)
            Quaternion currentLookRotation = isCaptured ? movement.LookRotation : input.LookRotation;

            // Transform input to world space relative to the Look Rotation
            Vector3 worldDirection = currentLookRotation * input.MovementDirection;
            worldDirection.y = 0;
            if (worldDirection.sqrMagnitude > 1) worldDirection.Normalize();

            // Rotate character to always face the look direction
            movement.SetRotation(Quaternion.Slerp(movement.Transform.rotation, currentLookRotation, 20f * deltaTime));

            // Determine Target Speed
            float targetSpeed = movement.WalkSpeed * (input.IsSprinting && ground.IsGrounded ? movement.SprintMultiplier : 1f);

            // 1. Calculate Horizontal Velocity with Momentum
            _horizontalVelocities[character.Id] = _processor.CalculateHorizontalVelocity(
                _horizontalVelocities[character.Id],
                worldDirection,
                targetSpeed,
                30f, // Acceleration
                20f, // Deceleration
                deltaTime);

            // 2. Calculate Vertical Velocity (Jump & Gravity)
            _verticalVelocities[character.Id] = _processor.CalculateVerticalVelocity(
                _verticalVelocities[character.Id],
                movement.Gravity,
                ground.IsGrounded,
                input.IsJumping,
                movement.JumpForce,
                deltaTime);

            // 3. Momentum Inheritance: If jumping, add the platform's velocity to our internal buffers
            if (input.IsJumping && ground.IsGrounded)
            {
                Vector3 platformVelocity = ground.GroundVelocity;
                _horizontalVelocities[character.Id] += new Vector3(platformVelocity.x, 0, platformVelocity.z);
                _verticalVelocities[character.Id] += platformVelocity.y;
            }

            // 4. Calculate Final Movement
            Vector3 intentionalMotion = (_horizontalVelocities[character.Id] + (Vector3.up * _verticalVelocities[character.Id])) * deltaTime;

            // 5. Apply Platform Push (Simplified logic as requested)
            Vector3 platformPush = ground.SurfaceDelta;
            Quaternion platformRotationPush = ground.RotationDelta;

            // The magic: Movement = Intentional Movement + Platform Push
            movement.Move(intentionalMotion + platformPush);

            // Apply Platform Rotation
            if (platformRotationPush != Quaternion.identity)
            {
                movement.SetRotation(platformRotationPush * movement.Transform.rotation);

                // Keep the camera orientation synchronized with the platform's rotation
                if (isCaptured && character.Look != null)
                {
                    float yawDelta = platformRotationPush.eulerAngles.y;
                    
                    // Normalize the euler angle to [-180, 180] to avoid jumping by 360 degrees
                    if (yawDelta > 180f) yawDelta -= 360f;

                    if (Mathf.Abs(yawDelta) > 0.001f)
                    {
                        character.Look.Yaw += yawDelta;
                    }
                }
            }
        }

        private GroundData ResolveGrounding(IHumanoidCharacterView character)
        {
            var movement = character.Movement;
            var ground = movement.CurrentGround;

            // Reset dynamic platform data
            ground.GroundTransform = null;
            ground.GroundVelocity = Vector3.zero;
            ground.SurfaceDelta = Vector3.zero;
            ground.RotationDelta = Quaternion.identity;

            // 1. Analyze Sensing data from the View
            if (!movement.LastGroundHit.HasValue) return ground;

            var hit = movement.LastGroundHit.Value;
            ground.GroundTransform = hit.transform;
            ground.GroundNormal = hit.normal;

            // 2. Identify Moving Platforms (Logic moved from Infrastructure to Application)
            var platform = hit.collider.GetComponentInParent<IMovingGround>();
            if (platform == null) return ground;

            // "Stickiness" Logic: If we sense a platform close enough, we apply its deltas
            // even if Unity's physics says we aren't technically grounded yet.
            ground.GroundVelocity = platform.Velocity;
            ground.RotationDelta = platform.RotationDelta;

            // Calculate rotational displacement
            // We use the Transform property of the hit object to get the platform's current pivot.
            // (Assuming the IMovingGround component is on the same object or we use the hit transform as the pivot)
            Transform platformTransform = hit.transform;

            // 1. Where was the platform's center before it moved this frame?
            Vector3 platformOldPos = platformTransform.position - platform.PositionDelta;

            // 2. What was the player's offset from that old center?
            Vector3 offsetFromOldPivot = movement.Transform.position - platformOldPos;

            // 3. Rotate the offset by the platform's rotation delta
            Vector3 rotatedOffset = platform.RotationDelta * offsetFromOldPivot;

            // 4. The new absolute position of the ground under the player's feet
            Vector3 newSpotPos = platformTransform.position + rotatedOffset;

            // 5. The total displacement for the player is the difference
            ground.SurfaceDelta = newSpotPos - movement.Transform.position;

            return ground;
        }
    }
}
