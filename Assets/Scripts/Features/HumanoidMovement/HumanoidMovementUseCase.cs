#nullable enable
using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using System.Collections.Generic;
using System;
using TinCan.Features.Abilities;
using TinCan.Features.Airship;
using TinCan.Features.FreeCamera;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Application Layer: Coordinates input and domain logic to move the humanoid character.
    /// Inherits from SimulationUseCase for unified actor simulation.
    /// </summary>
    public class HumanoidMovementUseCase : SimulationUseCase<IHumanoidCharacterView, HumanoidInputState>, IHumanoidRespawnService
    {
        private readonly HumanoidMovementProcessor _processor;
        private readonly AbilitySystemUseCase _abilitySystem;
        // Horizontal momentum, in the yaw frame of _velocityFrames[id] (the platform underfoot; world when absent).
        private readonly Dictionary<Guid, Vector3> _horizontalVelocities = new();
        private readonly Dictionary<Guid, Transform?> _velocityFrames = new();
        private readonly Dictionary<Guid, float> _verticalVelocities = new();
        private readonly Dictionary<Guid, ulong> _previousInputMasks = new();
        private readonly Collider[] _platformOverlapResults = new Collider[16];

        private struct PlatformPose
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private readonly Dictionary<Guid, Transform> _lastPlatforms = new();
        private readonly Dictionary<Guid, PlatformPose> _platformPoses = new();

        // Prediction (owner) and authority (server) bookkeeping; see IPredictedHumanoid.
        private readonly HumanoidReconciliationProcessor _reconciliation = new();
        private readonly Dictionary<Guid, HumanoidPredictionHistory> _histories = new();
        private readonly Dictionary<Guid, byte> _teleportEpochs = new();

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

        /// <summary>
        /// Advances every buffered (server-side, remote-owned) input stream by exactly one input before simulating,
        /// so the server applies each client tick's input once and in order.
        /// </summary>
        public override void Tick()
        {
            foreach (var actor in Registry.GetActors<IHumanoidCharacterView>())
            {
                if (actor is IBufferedInputSource buffered) buffered.AdvanceInput();
            }

            base.Tick();
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
                // Relative to the platform underfoot, so the server applies it against its own view of that platform.
                LookRotation = Quaternion.Inverse(FrameYaw(movement.CurrentGround.MovingGroundTransform)) * movement.LookRotation,
                ActiveInputMask = InputService.GetActiveInputMask()
            };
        }

        protected override void ProcessSimulation(IHumanoidCharacterView character, HumanoidInputState input, bool isCaptured)
        {
            var predicted = character as IPredictedHumanoid;
            bool isPredictingOwner = isCaptured && predicted is { IsLocallyPredicted: true };

            // 0. Owner: fold in the newest server state before predicting this tick's input.
            if (isPredictingOwner) Reconcile(character, predicted!);

            if (!_previousInputMasks.TryGetValue(character.Id, out ulong prevMask))
            {
                prevMask = 0;
            }

            // 1. Process Abilities first (Ensures prediction of tags/attributes for movement)
            _abilitySystem.ProcessAbilitySimulation(character, input, prevMask, TimeService.DeltaTime);

            // Store the mask for the next tick
            _previousInputMasks[character.Id] = input.ActiveInputMask;

            SimulateMovement(character, input, isCaptured);

            // 4. Owner remembers what it predicted for this input; the server reports what it actually did.
            if (isPredictingOwner)
            {
                History(character.Id).Record(input, CaptureState(character, input.Sequence));
            }
            else if (!isCaptured && predicted is { PublishesAuthoritativeState: true })
            {
                predicted.PublishAuthoritativeState(CaptureState(character, input.Sequence));
            }
        }

        public void ResetCharacter(IHumanoidCharacterView character, Vector3 position, Quaternion rotation)
        {
            character.Movement.SetPose(position, rotation);
            character.InputState = default;
            _horizontalVelocities.Remove(character.Id);
            _velocityFrames.Remove(character.Id);
            _verticalVelocities.Remove(character.Id);
            _previousInputMasks.Remove(character.Id);
            ClearPlatformState(character.Id);

            // A teleport: the owner must snap to the next server state instead of reconciling across it.
            _teleportEpochs[character.Id] = (byte)(TeleportEpoch(character.Id) + 1);
            History(character.Id).Clear();
        }

        /// <summary>
        /// Carries a locally predicted owner with the platform it stands on, every rendered frame. The owner only
        /// simulates on network ticks, while its ship is interpolated every frame. Without this the player would step
        /// against the deck at the tick rate. The platform pose cache is advanced too, so the next tick's
        /// SurfaceDelta covers only what remains.
        /// </summary>
        public void CarryWithPlatform(IHumanoidCharacterView character)
        {
            if (!_lastPlatforms.TryGetValue(character.Id, out var platform) || platform == null) return;
            if (!_platformPoses.TryGetValue(character.Id, out var pose)) return;

            var movement = character.Movement;
            Vector3 local = Quaternion.Inverse(pose.Rotation) * (movement.Transform.position - pose.Position);
            Vector3 carried = platform.rotation * local + platform.position;
            Quaternion rotationDelta = platform.rotation * Quaternion.Inverse(pose.Rotation);
            _platformPoses[character.Id] = new PlatformPose { Position = platform.position, Rotation = platform.rotation };

            movement.Carry(carried - movement.Transform.position);
            ApplyPlatformYaw(character, rotationDelta, isCaptured: true);
        }

        private void Reconcile(IHumanoidCharacterView character, IPredictedHumanoid predicted)
        {
            if (!predicted.TryTakeAuthoritativeState(out var server)) return;

            var history = History(character.Id);
            bool hasPrediction = history.TryGet(server.Sequence, out var mine);
            if (hasPrediction && mine.Platform != null && server.Platform != null && mine.Platform != server.Platform)
            {
                // Two transforms on the same rigid platform (moving-ground component vs NetworkObject root): rebase.
                mine = HumanoidAuthoritativeState.FromWorld(mine.Sequence, mine.TeleportEpoch, server.Platform,
                    mine.WorldPosition, mine.WorldHorizontalVelocity, mine.VerticalVelocity);
            }

            var decision = _reconciliation.Evaluate(server, hasPrediction, mine, TeleportEpoch(character.Id));
            var stats = predicted.PredictionStats;

            switch (decision.Action)
            {
                case ReconciliationAction.Ignore:
                    return;
                case ReconciliationAction.Match:
                    stats.Acks++;
                    stats.Matches++;
                    history.DropThrough(server.Sequence);
                    break;
                case ReconciliationAction.Correct:
                    stats.Acks++;
                    stats.Corrections++;
                    history.DropThrough(server.Sequence);
                    Vector3 before = character.Movement.Transform.position;
                    RewindAndReplay(character, server, history);
                    float moved = Vector3.Distance(before, character.Movement.Transform.position);
                    stats.CorrectionSum += moved;
                    stats.MaxCorrection = Mathf.Max(stats.MaxCorrection, moved);
                    break;
                case ReconciliationAction.Snap:
                    stats.Acks++;
                    stats.Snaps++;
                    SnapTo(character, server);
                    history.Clear();
                    _teleportEpochs[character.Id] = server.TeleportEpoch;
                    break;
            }
        }

        /// <summary>
        /// Puts the owner back into the server's state after input N and re-simulates every newer unacknowledged
        /// input with the same motion step, in the platform's current frame (the platform is held still: replay is
        /// ship-relative). Abilities are not re-run and the platform pose cache is left alone.
        /// </summary>
        private void RewindAndReplay(IHumanoidCharacterView character, HumanoidAuthoritativeState server, HumanoidPredictionHistory history)
        {
            var movement = character.Movement;

            // Carry, not SetPose: re-enabling the CharacterController clears isGrounded.
            movement.Carry(server.WorldPosition - movement.Transform.position);
            _velocityFrames[character.Id] = server.Platform;
            _horizontalVelocities[character.Id] = Quaternion.Inverse(FrameYaw(server.Platform)) * server.WorldHorizontalVelocity;
            _verticalVelocities[character.Id] = server.VerticalVelocity;
            Physics.SyncTransforms();

            var entries = history.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                movement.RefreshSensing();
                var ground = movement.CurrentGround;
                ground.IsPlatformSupported = IsOnMovingGround(movement);
                ground.MovingGroundTransform = VelocityFrame(character.Id);

                IntegrateMotion(character, entries[i].Input, ground, Vector3.zero, TimeService.DeltaTime);
                history.Replace(i, CaptureState(character, entries[i].State.Sequence));
            }
        }

        private static bool IsOnMovingGround(IHumanoidMovementView movement) =>
            movement.LastGroundHit.HasValue && movement.LastGroundHit.Value.collider.GetComponentInParent<IMovingGround>() != null;

        private void SnapTo(IHumanoidCharacterView character, HumanoidAuthoritativeState server)
        {
            var movement = character.Movement;
            movement.SetPose(server.WorldPosition, movement.Transform.rotation);
            _velocityFrames[character.Id] = server.Platform;
            _horizontalVelocities[character.Id] = Quaternion.Inverse(FrameYaw(server.Platform)) * server.WorldHorizontalVelocity;
            _verticalVelocities[character.Id] = server.VerticalVelocity;
        }

        private HumanoidAuthoritativeState CaptureState(IHumanoidCharacterView character, uint sequence)
        {
            var movement = character.Movement;
            return HumanoidAuthoritativeState.FromWorld(
                sequence,
                TeleportEpoch(character.Id),
                movement.CurrentGround.MovingGroundTransform,
                movement.Transform.position,
                HorizontalVelocity(character.Id),
                VerticalVelocity(character.Id));
        }

        private HumanoidPredictionHistory History(Guid id)
        {
            if (!_histories.TryGetValue(id, out var history))
            {
                history = new HumanoidPredictionHistory();
                _histories[id] = history;
            }

            return history;
        }

        private byte TeleportEpoch(Guid id) => _teleportEpochs.TryGetValue(id, out var epoch) ? epoch : (byte)0;
        private Vector3 LocalHorizontalVelocity(Guid id) => _horizontalVelocities.TryGetValue(id, out var velocity) ? velocity : Vector3.zero;
        private Transform? VelocityFrame(Guid id) => _velocityFrames.TryGetValue(id, out var frame) ? frame : null;
        private Vector3 HorizontalVelocity(Guid id) => FrameYaw(VelocityFrame(id)) * LocalHorizontalVelocity(id);
        private float VerticalVelocity(Guid id) => _verticalVelocities.TryGetValue(id, out var velocity) ? velocity : 0f;

        private void SimulateMovement(IHumanoidCharacterView character, HumanoidInputState input, bool isCaptured)
        {

            var movement = character.Movement;

            // 2. Resolve grounding and platforms
            movement.RefreshSensing();
            var ground = ResolveGrounding(character);
            movement.UpdateGroundData(ground);

            float deltaTime = TimeService.DeltaTime;

            // Initialize velocity tracking for this specific actor if missing
            if (!_verticalVelocities.ContainsKey(character.Id)) _verticalVelocities[character.Id] = 0f;

            // Simulate in the yaw frame of the platform underfoot (world when none). The input's look is relative to
            // that frame and momentum is kept in it, so owner and server move identically relative to the deck even
            // though each sees the ship at a different heading (the owner's ship is interpolated behind the server's).
            SetVelocityFrame(character.Id, ground.MovingGroundTransform);
            Quaternion currentLookRotation = FrameYaw(ground.MovingGroundTransform) * input.LookRotation;

            // Rotate character to always face the look direction
            movement.SetRotation(Quaternion.Slerp(movement.Transform.rotation, currentLookRotation, 20f * deltaTime));

            // The magic: Movement = Intentional Movement + Surface Delta (from platform)
            IntegrateMotion(character, input, ground, ground.SurfaceDelta, deltaTime);

            ApplyPlatformYaw(character, ground.RotationDelta, isCaptured);
        }

        /// <summary>
        /// One tick of momentum, jump and gravity from <paramref name="input"/>, then the move. Shared by live
        /// simulation and prediction replay so both step identically. Horizontal momentum is in the yaw frame of
        /// <see cref="GroundData.MovingGroundTransform"/>.
        /// </summary>
        private void IntegrateMotion(IHumanoidCharacterView character, HumanoidInputState input, GroundData ground, Vector3 extraMotion, float deltaTime)
        {
            var movement = character.Movement;
            Quaternion frameYaw = FrameYaw(ground.MovingGroundTransform);

            Vector3 localDirection = input.LookRotation * input.MovementDirection;
            localDirection.y = 0;
            if (localDirection.sqrMagnitude > 1) localDirection.Normalize();

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
                LocalHorizontalVelocity(character.Id),
                localDirection,
                targetSpeed,
                30f, // Acceleration
                20f, // Deceleration
                deltaTime);

            // 2. Calculate Vertical Velocity (Jump & Gravity)
            _verticalVelocities[character.Id] = _processor.CalculateVerticalVelocity(
                VerticalVelocity(character.Id),
                movement.Gravity,
                ground.IsGrounded,
                ground.IsPlatformSupported,
                input.IsJumping,
                jumpForce,
                deltaTime);

            // 3. Calculate Final Movement
            Vector3 intentionalMotion = (frameYaw * _horizontalVelocities[character.Id] + (Vector3.up * _verticalVelocities[character.Id])) * deltaTime;
            movement.Move(intentionalMotion + extraMotion);
        }

        private static void ApplyPlatformYaw(IHumanoidCharacterView character, Quaternion rotationDelta, bool isCaptured)
        {
            if (rotationDelta == Quaternion.identity) return;

            // Isolate the Yaw (Y-axis) rotation from the platform's full 3D rotation delta
            // This ensures characters standing on banking airships or slanted platforms don't lean sideways
            float yawDelta = rotationDelta.eulerAngles.y;
            if (yawDelta > 180f) yawDelta -= 360f; // Normalize to [-180, 180]

            var movement = character.Movement;
            movement.SetRotation(Quaternion.Euler(0f, yawDelta, 0f) * movement.Transform.rotation);

            // Keep the camera orientation synchronized with the platform's rotation
            if (isCaptured && character is IHasOrbitalCamera hasCamera && hasCamera.Look != null && Mathf.Abs(yawDelta) > 0.001f)
            {
                hasCamera.Look.Yaw += yawDelta;
            }
        }

        private GroundData ResolveGrounding(IHumanoidCharacterView character)
        {
            var movement = character.Movement;
            var ground = movement.CurrentGround;

            // Reset dynamic platform data for this frame
            ground.GroundTransform = null;
            ground.MovingGroundTransform = null;
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
                ground.GroundTransform = hit.collider.transform;

                movingGround = hit.collider.GetComponentInParent<IMovingGround>();
                if (movingGround != null)
                {
                    platformTransform = ((Component)movingGround).transform;
                    ground.GroundVelocity = movingGround.Velocity;
                    // Feet on a moving deck count as support even on the jump tick: CharacterController.isGrounded is
                    // unreliable while the deck moves, and clearing this on the jump tick swallowed the jump itself.
                    ground.IsPlatformSupported = true;
                }
            }

            // Vehicle volume membership controls relative motion independently of foot contact.
            if (TryResolveContainingParent(movement.Transform.position, out var volumePlatform, out var volumeMovingGround) &&
                volumeMovingGround != null)
            {
                platformTransform = volumePlatform;
                movingGround = volumeMovingGround;
                ground.GroundVelocity = movingGround.Velocity;
            }

            if (platformTransform == null && _lastPlatforms.TryGetValue(character.Id, out var lastPlat) && lastPlat != null)
            {
                var localSpaceVolume = lastPlat.GetComponent<ParentLocalSpaceVolume>();
                if (localSpaceVolume != null && localSpaceVolume.Contains(movement.Transform.position))
                {
                    platformTransform = lastPlat;
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

            ground.MovingGroundTransform = platformTransform;

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

        private bool TryResolveContainingParent(
            Vector3 worldPosition,
            out Transform? platformTransform,
            out IMovingGround? movingGround)
        {
            platformTransform = null;
            movingGround = null;

            int overlapCount = Physics.OverlapSphereNonAlloc(
                worldPosition,
                0.01f,
                _platformOverlapResults,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int index = 0; index < overlapCount; index++)
            {
                var localSpaceVolume = _platformOverlapResults[index].GetComponentInParent<ParentLocalSpaceVolume>();
                if (localSpaceVolume == null || !localSpaceVolume.Contains(worldPosition)) continue;

                movingGround = localSpaceVolume.GetComponentInParent<IMovingGround>();
                if (movingGround == null) continue;

                platformTransform = ((Component)movingGround).transform;
                return true;
            }

            return false;
        }

        private void DetachFromPlatform(Guid characterId, Transform platformTransform, Vector3 worldPosition)
        {
            var movingGround = platformTransform.GetComponent<IMovingGround>();
            if (movingGround == null) return;

            Vector3 velocity = movingGround is IPointVelocityMovingGround pointVelocityGround
                ? pointVelocityGround.GetPointVelocity(worldPosition)
                : movingGround.Velocity;

            // Leaving the platform: momentum goes back to world space, plus the platform's own velocity.
            SetVelocityFrame(characterId, null);
            _horizontalVelocities[characterId] = LocalHorizontalVelocity(characterId) + new Vector3(velocity.x, 0, velocity.z);
            _verticalVelocities[characterId] = VerticalVelocity(characterId) + velocity.y;
        }

        private static Quaternion FrameYaw(Transform? frame) =>
            frame != null ? Quaternion.Euler(0f, frame.eulerAngles.y, 0f) : Quaternion.identity;

        /// <summary>Re-expresses stored horizontal momentum in a new frame (boarding or leaving a platform).</summary>
        private void SetVelocityFrame(Guid characterId, Transform? frame)
        {
            _velocityFrames.TryGetValue(characterId, out var current);
            if (current == frame) return;

            Vector3 world = FrameYaw(current) * LocalHorizontalVelocity(characterId);
            _horizontalVelocities[characterId] = Quaternion.Inverse(FrameYaw(frame)) * world;
            _velocityFrames[characterId] = frame;
        }

        private void ClearPlatformState(Guid characterId)
        {
            _lastPlatforms.Remove(characterId);
            _platformPoses.Remove(characterId);
        }
    }
}

