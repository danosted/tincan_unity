using UnityEngine;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Possession;
using System.Collections.Generic;
using System;
using System.Linq;

using TinCan.Features.Abilities;
using TinCan.Features.FreeCamera;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Application Layer: Coordinates input and domain logic to move the humanoid character.
    /// Inherits from SimulationUseCase for unified actor simulation.
    /// </summary>
    public class HumanoidMovementUseCase : SimulationUseCase<IHumanoidCharacterView, HumanoidInputState>
    {
        private readonly HumanoidMovementProcessor _processor;
        private readonly AbilitySystemUseCase _abilitySystem;
        private readonly Dictionary<Guid, Vector3> _horizontalVelocities = new();
        private readonly Dictionary<Guid, float> _verticalVelocities = new();
        private readonly Dictionary<Guid, ulong> _previousInputMasks = new();

        // Platform tracking to avoid execution order bugs with deltas
        private readonly Dictionary<Guid, Transform> _lastPlatforms = new();
        private readonly Dictionary<Guid, Vector3> _lastPlatformPositions = new();
        private readonly Dictionary<Guid, Quaternion> _lastPlatformRotations = new();

        public HumanoidMovementUseCase(
            IInputService inputService,
            INetworkService networkService,
            HumanoidMovementProcessor processor,
            AbilitySystemUseCase abilitySystem,
            IActorRegistry registry,
            ITimeService timeService)
            : base(inputService, networkService, registry, timeService)
        {
            _processor = processor;
            _abilitySystem = abilitySystem;
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
                LookRotation = character.Movement.LookRotation,
                ActiveInputMask = InputService.GetActiveInputMask()
            };
        }

        protected override void ProcessSimulation(IHumanoidCharacterView character, HumanoidInputState input, bool isCaptured)
        {
            if (!_previousInputMasks.TryGetValue(character.Id, out ulong prevMask))
            {
                prevMask = 0;
            }

            // 1. Process Abilities first (Ensures prediction of tags/attributes for movement)
            _abilitySystem.ProcessAbilitySimulation(character, input, prevMask, TimeService.DeltaTime);

            // Store the mask for the next tick
            _previousInputMasks[character.Id] = input.ActiveInputMask;

            var movement = character.Movement;

            // 2. Resolve grounding and platforms
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

            // Determine Target Speed from Attributes
            float targetSpeed = movement.WalkSpeed;
            float jumpForce = movement.JumpForce;

            var attributes = character.GetAttributeSet();
            if (attributes != null)
            {
                targetSpeed = attributes.MoveSpeed;
                jumpForce = attributes.JumpForce;
            }

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
                jumpForce,
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

            // The magic: Movement = Intentional Movement
            // We no longer manually apply surface delta to the controller here.
            // If the platform is truly kinematic and we rely on standard Unity nesting or continuous collision,
            // standard physics / character controller may sweep with the platform.
            // BUT, if we explicitly need to move with the platform, we add it back:
            movement.Move(intentionalMotion + ground.SurfaceDelta);

            // Apply Platform Rotation
            if (ground.RotationDelta != Quaternion.identity)
            {
                // Isolate the Yaw (Y-axis) rotation from the platform's full 3D rotation delta
                // This ensures characters standing on banking airships or slanted platforms don't lean sideways
                float yawDelta = ground.RotationDelta.eulerAngles.y;
                if (yawDelta > 180f) yawDelta -= 360f; // Normalize to [-180, 180]

                Quaternion yawOnlyDelta = Quaternion.Euler(0f, yawDelta, 0f);
                movement.SetRotation(yawOnlyDelta * movement.Transform.rotation);

                // Keep the camera orientation synchronized with the platform's rotation
                if (isCaptured && character is IHasOrbitalCamera hasCamera && hasCamera.Look != null)
                {
                    if (Mathf.Abs(yawDelta) > 0.001f)
                    {
                        hasCamera.Look.Yaw += yawDelta;
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
            if (platform == null)
            {
                _lastPlatforms.Remove(character.Id);
                return ground;
            }

            Transform platformTransform = hit.transform;

            // Retrieve precise current transform
            Vector3 currentPos = platformTransform.position;
            Quaternion currentRot = platformTransform.rotation;

            // Calculate precise Deltas based on our OWN cache to avoid execution order issues
            Vector3 positionDelta = Vector3.zero;
            Quaternion rotationDelta = Quaternion.identity;

            if (_lastPlatforms.TryGetValue(character.Id, out var lastPlatform) && lastPlatform == platformTransform)
            {
                positionDelta = currentPos - _lastPlatformPositions[character.Id];
                rotationDelta = currentRot * Quaternion.Inverse(_lastPlatformRotations[character.Id]);
            }

            // Update cache
            _lastPlatforms[character.Id] = platformTransform;
            _lastPlatformPositions[character.Id] = currentPos;
            _lastPlatformRotations[character.Id] = currentRot;

            // "Stickiness" Logic: We apply computed deltas even if Unity's physics says not grounded yet
            ground.GroundVelocity = platform.Velocity; // Logical velocity for jump momentum
            ground.RotationDelta = rotationDelta;

            // Calculate rotational and translational displacement
            // 1. Where was the platform's center before it moved this frame?
            Vector3 platformOldPos = currentPos - positionDelta;

            // 2. What was the player's offset from that old center?
            Vector3 offsetFromOldPivot = movement.Transform.position - platformOldPos;

            // 3. Rotate the offset by the platform's rotation delta
            Vector3 rotatedOffset = rotationDelta * offsetFromOldPivot;

            // 4. The new absolute position of the ground under the player's feet
            Vector3 newSpotPos = currentPos + rotatedOffset;

            // 5. The total displacement for the player is the difference
            ground.SurfaceDelta = newSpotPos - movement.Transform.position;

            return ground;
        }
    }
}
