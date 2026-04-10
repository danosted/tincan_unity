using UnityEngine;
using VContainer.Unity;
using TinCan.Core.Domain;
using System.Collections.Generic;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Application Layer: Coordinates input and domain logic to move the humanoid character.
    /// Implements ITickable for the VContainer update loop.
    /// </summary>
    public class HumanoidMovementUseCase : ITickable
    {
        private readonly IInputService _inputService;
        private readonly HumanoidMovementProcessor _processor;
        private readonly IEnumerable<IHumanoidMovementView> _views;

        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        public HumanoidMovementUseCase(
            IInputService inputService,
            HumanoidMovementProcessor processor,
            IEnumerable<IHumanoidMovementView> views)
        {
            _inputService = inputService;
            _processor = processor;
            _views = views;
        }

        public void Tick()
        {
            foreach (var view in _views)
            {
                if (!view.IsActive) continue;

                HandleMovement(view);
            }
        }

        private void HandleMovement(IHumanoidMovementView view)
        {
            // Calculate Input
            float horizontal = _inputService.GetAxis(ActionNames.MoveRight, ActionNames.MoveLeft);
            float vertical = _inputService.GetAxis(ActionNames.MoveForward, ActionNames.MoveBackward);
            Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;

            // Transform input to world space relative to the Look Rotation
            Vector3 worldDirection = view.LookRotation * inputDirection;
            worldDirection.y = 0;
            if (worldDirection.sqrMagnitude > 1) worldDirection.Normalize();

            // Rotate character to face movement direction if moving
            if (inputDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(worldDirection);
                view.SetRotation(Quaternion.Slerp(view.Transform.rotation, targetRotation, 10f * Time.deltaTime));
            }

            // Determine Target Speed
            bool isSprinting = _inputService.IsActionPressed(ActionNames.Sprint);
            float targetSpeed = view.WalkSpeed * (isSprinting && view.IsGrounded ? view.SprintMultiplier : 1f);

            // Calculate Horizontal Velocity with Momentum
            _horizontalVelocity = _processor.CalculateHorizontalVelocity(
                _horizontalVelocity,
                worldDirection,
                targetSpeed,
                30f, // Acceleration
                20f, // Deceleration
                Time.deltaTime);

            // Calculate Vertical Velocity (Jump & Gravity)
            bool jumpTriggered = _inputService.WasActionTriggered(ActionNames.Jump);
            _verticalVelocity = _processor.CalculateVerticalVelocity(
                _verticalVelocity,
                view.Gravity,
                view.IsGrounded,
                jumpTriggered,
                view.JumpForce,
                Time.deltaTime);

            // Apply Total Movement
            Vector3 totalVelocity = _horizontalVelocity + (Vector3.up * _verticalVelocity);
            view.Move(totalVelocity * Time.deltaTime);
        }
    }
}
