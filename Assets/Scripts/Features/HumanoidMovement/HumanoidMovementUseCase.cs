#nullable enable
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
        // Toggle to easily switch between the new Local-to-World matrix math and the legacy vector math
        public bool UseLocalToWorldCalculation = true;

        private readonly HumanoidMovementProcessor _processor;
        private readonly AbilitySystemUseCase _abilitySystem;
        private readonly Dictionary<Guid, Vector3> _horizontalVelocities = new();
        private readonly Dictionary<Guid, float> _verticalVelocities = new();
        private readonly Dictionary<Guid, ulong> _previousInputMasks = new();

        // Platform tracking to avoid execution order bugs with deltas
        private readonly Dictionary<Guid, Transform> _lastPlatforms = new();
        private readonly Dictionary<Guid, Vector3> _lastPlatformPositions = new();
        private readonly Dictionary<Guid, Quaternion> _lastPlatformRotations = new();
        private readonly Dictionary<Guid, float> _platformRetentionTimers = new();
        private const float PLATFORM_RETENTION_TIME = 0.5f;

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
            var ground = ResolveGrounding(character);
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

            // 3. Calculate Final Movement
            Vector3 intentionalMotion = (_horizontalVelocities[character.Id] + (Vector3.up * _verticalVelocities[character.Id])) * deltaTime;

            // The magic: Movement = Intentional Movement + Surface Delta (from platform)
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

            // Reset dynamic platform data for this frame
            ground.GroundTransform = null;
            ground.GroundVelocity = Vector3.zero;
            ground.SurfaceDelta = Vector3.zero;
            ground.RotationDelta = Quaternion.identity;

            Transform? platformTransform = null;
            IMovingGround? movingGround = null;

            // 1. Detect if we are standing on something
            if (movement.LastGroundHit.HasValue)
            {
                var hit = movement.LastGroundHit.Value;
                ground.GroundNormal = hit.normal;

                // Check for moving platforms
                movingGround = hit.collider.GetComponentInParent<IMovingGround>();
                if (movingGround != null)
                {
                    // FIX: Track the ROOT of the moving ground (the component itself)
                    // instead of the specific child collider hit. This prevents resets when
                    // walking across different colliders (stairs, floors) on the same ship.
                    platformTransform = ((Component)movingGround).transform;
                    ground.GroundTransform = platformTransform;
                    ground.GroundVelocity = movingGround.Velocity;

                    // Reset retention timer while grounded
                    _platformRetentionTimers[character.Id] = PLATFORM_RETENTION_TIME;
                }
            }

            // 2. Check for Airborne Retention (Coyote Time for platforms)
            // If we aren't hitting the ground, but we were recently on a platform, keep tracking it.
            if (platformTransform == null && _lastPlatforms.TryGetValue(character.Id, out var lastPlat) && lastPlat != null)
            {
                if (_platformRetentionTimers.TryGetValue(character.Id, out float timer) && timer > 0)
                {
                    platformTransform = lastPlat;
                    _platformRetentionTimers[character.Id] -= TimeService.DeltaTime;

                    // If it's the airship, we might still want its velocity
                    movingGround = platformTransform.GetComponent<IMovingGround>();
                    if (movingGround != null)
                    {
                        ground.GroundVelocity = movingGround.Velocity;
                    }
                }
                else
                {
                    // Retention expired! This is the moment of true detachment.
                    // Transfer the platform's velocity to the player's internal momentum.
                    if (movingGround == null) movingGround = lastPlat.GetComponent<IMovingGround>();
                    if (movingGround != null)
                    {
                        Vector3 vel = movingGround.Velocity;
                        _horizontalVelocities[character.Id] += new Vector3(vel.x, 0, vel.z);
                        _verticalVelocities[character.Id] += vel.y;
                    }

                    _lastPlatforms.Remove(character.Id);
                    _lastPlatformPositions.Remove(character.Id);
                    _lastPlatformRotations.Remove(character.Id);
                    _platformRetentionTimers.Remove(character.Id);
                    return ground;
                }
            }

            if (platformTransform == null)
            {
                _lastPlatforms.Remove(character.Id);
                return ground;
            }

            // 3. Calculate Deltas
            Vector3 currentPos = platformTransform.position;
            Quaternion currentRot = platformTransform.rotation;

            Vector3 oldPlatformPos = currentPos;
            Quaternion oldPlatformRot = currentRot;

            if (_lastPlatforms.TryGetValue(character.Id, out var cachedPlat) && cachedPlat == platformTransform)
            {
                oldPlatformPos = _lastPlatformPositions[character.Id];
                oldPlatformRot = _lastPlatformRotations[character.Id];

                ground.RotationDelta = currentRot * Quaternion.Inverse(oldPlatformRot);
            }

            // Update cache
            _lastPlatforms[character.Id] = platformTransform;
            _lastPlatformPositions[character.Id] = currentPos;
            _lastPlatformRotations[character.Id] = currentRot;

            // Compute Surface Displacement
            if (UseLocalToWorldCalculation)
            {
                // Character-Anchor Matrix Transformation
                Matrix4x4 oldMatrix = Matrix4x4.TRS(oldPlatformPos, oldPlatformRot, platformTransform.lossyScale);
                Vector3 charLocalPos = oldMatrix.inverse.MultiplyPoint3x4(movement.Transform.position);
                Vector3 expectedWorldPos = platformTransform.TransformPoint(charLocalPos);
                ground.SurfaceDelta = expectedWorldPos - movement.Transform.position;
            }
            else
            {
                // Vector Offset Rotation (Legacy)
                Vector3 offsetFromOldPivot = movement.Transform.position - oldPlatformPos;
                Vector3 rotatedOffset = ground.RotationDelta * offsetFromOldPivot;
                Vector3 expectedWorldPos = currentPos + rotatedOffset;
                ground.SurfaceDelta = expectedWorldPos - movement.Transform.position;
            }

            return ground;
        }
    }
}

