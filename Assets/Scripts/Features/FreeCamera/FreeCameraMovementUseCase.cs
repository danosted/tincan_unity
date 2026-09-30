#nullable enable
using UnityEngine;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;

namespace TinCan.Features.FreeCamera
{
    /// <summary>
    /// Application Layer: Coordinates input and logic to move the camera view.
    /// Implements ITickable to run in the VContainer-managed update loop. Reads the Free Camera context's Move and the
    /// Camera context's Look; both read zero while their context is not live.
    /// </summary>
    public class FreeCameraMovementUseCase : ITickable
    {
        private readonly IInputReader _input;
        private readonly FreeCameraInputContext _controls;
        private readonly CameraInputContext _camera;
        private readonly FreeCameraMovementProcessor _moveProcessor;
        private readonly FreeCameraRotationProcessor _rotationProcessor;
        private readonly IActorRegistry _registry;

        public FreeCameraMovementUseCase(
            IInputReader input,
            FreeCameraInputContext controls,
            CameraInputContext camera,
            FreeCameraMovementProcessor moveProcessor,
            FreeCameraRotationProcessor rotationProcessor,
            IActorRegistry registry)
        {
            _input = input;
            _controls = controls;
            _camera = camera;
            _moveProcessor = moveProcessor;
            _rotationProcessor = rotationProcessor;
            _registry = registry;
        }

        public void Tick()
        {
            foreach (var view in _registry.GetActors<IFreeCameraView>())
            {
                if (!view.IsActive) continue;

                HandleRotation(view);
                HandleMovement(view);
            }
        }

        private void HandleRotation(IFreeCameraView view)
        {
            Vector2 mouseDelta = _input.ReadVector2(_camera.Look);
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
            Vector2 move = _input.ReadVector2(_controls.Move);
            Vector3 inputDirection = new Vector3(move.x, 0, move.y).normalized;
            if (inputDirection.sqrMagnitude < 0.001f) return;

            // Transform input direction to camera-relative world direction
            Vector3 worldDirection = view.CameraTransform.TransformDirection(inputDirection);

            Vector3 displacement = _moveProcessor.CalculateDisplacement(
                worldDirection,
                view.MoveSpeed,
                Time.deltaTime);

            view.CameraTransform.position += displacement;
        }
    }
}
