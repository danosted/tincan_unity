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
using TinCan.Features.Airship;
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

        private struct PlatformPose
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private readonly Dictionary<Guid, Transform> _lastPlatforms = new();
        private readonly Dictionary<Guid, PlatformPose> _platformPoses = new();

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

            SimulateMovement(character, input, isCaptured);
        }

        public HumanoidMovementSnapshot CaptureSnapshot(IHumanoidCharacterView character, uint lastProcessedInputSequence)
        {
            return new HumanoidMovementSnapshot
            {
                LastProcessedInputSequence = lastProcessedInputSequence,
                Position = character.Movement.Transform.position,
                Rotation = character.Movement.Transform.rotation,
                HorizontalVelocity = _horizontalVelocities.GetValueOrDefault(character.Id),
                VerticalVelocity = _verticalVelocities.GetValueOrDefault(character.Id),
                PreviousInputMask = _previousInputMasks.GetValueOrDefault(character.Id)
            };
        }

        public void Reconcile(IHumanoidCharacterView character, HumanoidMovementSnapshot snapshot, IReadOnlyList<HumanoidInputState> pendingInputs)
        {
            character.Movement.SetPose(snapshot.Position, snapshot.Rotation);
            _horizontalVelocities[character.Id] = snapshot.HorizontalVelocity;
            _verticalVelocities[character.Id] = snapshot.VerticalVelocity;
            _previousInputMasks[character.Id] = snapshot.PreviousInputMask;
            ClearPlatformState(character.Id);

            foreach (var input in pendingInputs)
            {
                SimulateMovement(character, input, false);
            }
        }

        private void SimulateMovement(IHumanoidCharacterView character, HumanoidInputState input, bool isCaptured)
        {

            var movement = character.Movement;

            // 2. Resolve grounding and platforms
            movement.RefreshSensing();
            var ground = ResolveGrounding(character, input.IsJumping);
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
                ground.IsPlatformSupported,
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

        private GroundData ResolveGrounding(IHumanoidCharacterView character, bool isJumping)
        {
            var movement = character.Movement;
            var ground = movement.CurrentGround;

            // Reset dynamic platform data for this frame
            ground.GroundTransform = null;
            ground.GroundVelocity = Vector3.zero;
            ground.SurfaceDelta = Vector3.zero;
            ground.RotationDelta = Quaternion.identity;
            ground.IsPlatformSupported = false;

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
                    platformTransform = ((Component)movingGround).transform;
                    ground.GroundTransform = platformTransform;
                    ground.GroundVelocity = movingGround.Velocity;
                    ground.IsPlatformSupported = !isJumping;
                }
            }

            if (platformTransform == null && _lastPlatforms.TryGetValue(character.Id, out var lastPlat) && lastPlat != null)
            {
                var localSpaceVolume = lastPlat.GetComponent<AirshipLocalSpaceVolume>();
                if (localSpaceVolume != null && localSpaceVolume.Contains(movement.Transform.position))
                {
                    platformTransform = lastPlat;
                    ground.GroundTransform = platformTransform;
                    movingGround = platformTransform.GetComponent<IMovingGround>();
                    if (movingGround != null)
                    {
                        ground.GroundVelocity = movingGround.Velocity;
                    }
                }
                else
                {
                    DetachFromPlatform(character.Id, lastPlat, movement.Transform.position);
                    ClearPlatformState(character.Id);
                    return ground;
                }
            }

            if (platformTransform == null)
            {
                ClearPlatformState(character.Id);
                return ground;
            }

            if (!_lastPlatforms.TryGetValue(character.Id, out var cachedPlatform) ||
                cachedPlatform != platformTransform ||
                !_platformPoses.TryGetValue(character.Id, out var previousPose))
            {
                _platformPoses[character.Id] = new PlatformPose
                {
                    Position = platformTransform.position,
                    Rotation = platformTransform.rotation
                };
            }
            else
            {
                Vector3 localPosition = Quaternion.Inverse(previousPose.Rotation) * (movement.Transform.position - previousPose.Position);
                Vector3 carriedWorldPosition = platformTransform.rotation * localPosition + platformTransform.position;
                ground.SurfaceDelta = carriedWorldPosition - movement.Transform.position;
                ground.RotationDelta = platformTransform.rotation * Quaternion.Inverse(previousPose.Rotation);

                _platformPoses[character.Id] = new PlatformPose
                {
                    Position = platformTransform.position,
                    Rotation = platformTransform.rotation
                };
            }

            _lastPlatforms[character.Id] = platformTransform;

            return ground;
        }

        private void DetachFromPlatform(Guid characterId, Transform platformTransform, Vector3 worldPosition)
        {
            var movingGround = platformTransform.GetComponent<IMovingGround>();
            if (movingGround == null) return;

            Vector3 velocity = movingGround is IPointVelocityMovingGround pointVelocityGround
                ? pointVelocityGround.GetPointVelocity(worldPosition)
                : movingGround.Velocity;

            _horizontalVelocities[characterId] += new Vector3(velocity.x, 0, velocity.z);
            _verticalVelocities[characterId] += velocity.y;
        }

        private void ClearPlatformState(Guid characterId)
        {
            _lastPlatforms.Remove(characterId);
            _platformPoses.Remove(characterId);
        }
    }
}

