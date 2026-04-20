using UnityEngine;
using VContainer.Unity;
using TinCan.Core.Domain;
using System.Collections.Generic;

namespace TinCan.Features.FreeCamera
{
    /// <summary>
    /// Application Layer: Coordinates input and logic to move the camera view.
    /// Implements ITickable to run in the VContainer-managed update loop.
    /// </summary>
    public class FreeCameraMovementUseCase : ITickable
    {
        private readonly IInputService _inputService;
        private readonly FreeCameraMovementProcessor _moveProcessor;
        private readonly FreeCameraRotationProcessor _rotationProcessor;
        private readonly IActorRegistry _registry;

        public FreeCameraMovementUseCase(
            IInputService inputService,
            FreeCameraMovementProcessor moveProcessor,
            FreeCameraRotationProcessor rotationProcessor,
            IActorRegistry registry)
        {
            _inputService = inputService;
            _moveProcessor = moveProcessor;
            _rotationProcessor = rotationProcessor;
            _registry = registry;
        }

        public void Tick()
        {
            bool anyActive = false;
            foreach (var view in _registry.GetActors<IFreeCameraView>())
            {
                if (!view.IsActive) continue;
                anyActive = true;

                HandleRotation(view);
                HandleMovement(view);
            }

            if (anyActive)
            {
                HandleCursorToggle();
            }
        }

        private void HandleCursorToggle()
        {
            if (_inputService.WasActionTriggered(ActionNames.Cancel))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        private void HandleRotation(IFreeCameraView view)
        {
            Vector2 mouseDelta = _inputService.GetMouseDelta();
            if (mouseDelta.sqrMagnitude < 0.001f) return;

            var result = _rotationProcessor.CalculateRotation(
                view.CurrentPitch,
                view.CurrentYaw,
                mouseDelta,
                view.RotationSensitivity,
                view.MaxPitchAngle);

            view.ApplyRotation(result.NewPitch, result.NewYaw);
        }

        private void HandleMovement(IFreeCameraView view)
        {
            Vector3 inputDirection = CalculateInputDirection();
            if (inputDirection.sqrMagnitude < 0.001f) return;

            // Transform input direction to camera-relative world direction
            Vector3 worldDirection = view.CameraTransform.TransformDirection(inputDirection);

            Vector3 displacement = _moveProcessor.CalculateDisplacement(
                worldDirection,
                view.MoveSpeed,
                Time.deltaTime);

            view.CameraTransform.position += displacement;
        }

        private Vector3 CalculateInputDirection()
        {
            float horizontal = _inputService.GetAxis(ActionNames.MoveRight, ActionNames.MoveLeft);
            float vertical = _inputService.GetAxis(ActionNames.MoveForward, ActionNames.MoveBackward);

            return new Vector3(horizontal, 0, vertical).normalized;
        }
    }
}
