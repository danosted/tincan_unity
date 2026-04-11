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
    /// Implements ITickable for the VContainer update loop.
    /// </summary>
    public class HumanoidMovementUseCase : ITickable
    {
        private readonly IInputService _inputService;
        private readonly INetworkService _networkService;
        private readonly HumanoidMovementProcessor _processor;
        private readonly IActorRegistry _registry;

        private Dictionary<Guid, Vector3> _horizontalVelocities = new();
        private Dictionary<Guid, float> _verticalVelocities = new();

        public HumanoidMovementUseCase(
            IInputService inputService,
            INetworkService networkService,
            HumanoidMovementProcessor processor,
            IActorRegistry registry)
        {
            _inputService = inputService;
            _networkService = networkService;
            _processor = processor;
            _registry = registry;
        }

        public void Tick()
        {
            // Process all complete characters (Facade pattern or Mediator)
            foreach (var character in _registry.GetActors<IHumanoidCharacterView>())
            {
                if (!character.IsSimulating) continue;
                HandleMovement(character);
            }
        }

        private void HandleMovement(IHumanoidCharacterView character)
        {
            var movement = character.Movement;
            var authority = character;

            // Initialize velocity tracking for this specific actor if missing
            if (!_horizontalVelocities.ContainsKey(character.Id)) _horizontalVelocities[character.Id] = Vector3.zero;
            if (!_verticalVelocities.ContainsKey(character.Id)) _verticalVelocities[character.Id] = 0f;

            Vector3 inputDirection = Vector3.zero;
            bool jumpTriggered = false;
            bool isSprinting = false;

            ulong localId = _networkService.LocalClientId;
            bool isCaptured = authority.IsCapturedBy(localId);

            if (isCaptured)
            {
                float horizontal = _inputService.GetAxis(ActionNames.MoveRight, ActionNames.MoveLeft);
                float vertical = _inputService.GetAxis(ActionNames.MoveForward, ActionNames.MoveBackward);
                inputDirection = new Vector3(horizontal, 0, vertical).normalized;
                jumpTriggered = _inputService.WasActionTriggered(ActionNames.Jump);
                isSprinting = _inputService.IsActionPressed(ActionNames.Sprint);
            }

            // Transform input to world space relative to the Look Rotation
            Vector3 worldDirection = movement.LookRotation * inputDirection;
            worldDirection.y = 0;
            if (worldDirection.sqrMagnitude > 1) worldDirection.Normalize();

            // Rotate character to face movement direction if moving
            if (inputDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(worldDirection);
                movement.SetRotation(Quaternion.Slerp(movement.Transform.rotation, targetRotation, 10f * Time.deltaTime));
            }

            // Determine Target Speed
            float targetSpeed = movement.WalkSpeed * (isSprinting && movement.CurrentGround.IsGrounded ? movement.SprintMultiplier : 1f);

            // Calculate Horizontal Velocity with Momentum
            _horizontalVelocities[authority.Id] = _processor.CalculateHorizontalVelocity(
                _horizontalVelocities[authority.Id],
                worldDirection,
                targetSpeed,
                30f, // Acceleration
                20f, // Deceleration
                Time.deltaTime);

            // Calculate Vertical Velocity (Jump & Gravity)
            _verticalVelocities[authority.Id] = _processor.CalculateVerticalVelocity(
                _verticalVelocities[authority.Id],
                movement.Gravity,
                movement.CurrentGround.IsGrounded,
                jumpTriggered,
                movement.JumpForce,
                Time.deltaTime);

            // Apply Total Movement: Local Motion + Ground Delta
            Vector3 relativeMotion = (_horizontalVelocities[authority.Id] + (Vector3.up * _verticalVelocities[authority.Id])) * Time.deltaTime;
            Vector3 finalMove = relativeMotion + movement.CurrentGround.SurfaceDelta;

            movement.Move(finalMove);
        }
    }
}
