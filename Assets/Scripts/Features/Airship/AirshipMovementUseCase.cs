using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using System.Collections.Generic;
using System;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Application Layer: Coordinates input and domain logic to simulate airship movement.
    /// </summary>
    public class AirshipMovementUseCase : SimulationUseCase<IAirshipView, AirshipInputState>
    {
        private readonly AirshipMovementProcessor _processor;
        private readonly Dictionary<Guid, Vector3> _linearVelocities = new();

        public AirshipMovementUseCase(
            IInputService inputService,
            INetworkService networkService,
            IActorRegistry registry,
            ITimeService timeService,
            AirshipMovementProcessor processor)
            : base(inputService, networkService, registry, timeService)
        {
            _processor = processor;
        }

        protected override AirshipInputState GatherLocalInput(IAirshipView airship)
        {
            // Pitch mapped to Jump (Up) and Sprint (Down) for testing
            float pitch = 0f;
            if (InputService.IsActionPressed(ActionNames.Jump)) pitch = 1f;
            else if (InputService.IsActionPressed(ActionNames.Sprint)) pitch = -1f;

            return new AirshipInputState
            {
                Throttle = InputService.GetAxis(ActionNames.MoveForward, ActionNames.MoveBackward),
                Yaw = InputService.GetAxis(ActionNames.MoveRight, ActionNames.MoveLeft),
                Pitch = pitch
            };
        }

        protected override void ProcessSimulation(IAirshipView airship, AirshipInputState input, bool isCaptured)
        {
            if (airship.IsControlsEnabled == false) return;
            if (!_linearVelocities.ContainsKey(airship.Id))
                _linearVelocities[airship.Id] = Vector3.zero;

            float deltaTime = TimeService.DeltaTime;

            // 1. Calculate Linear Velocity
            _linearVelocities[airship.Id] = _processor.CalculateLinearVelocity(
                _linearVelocities[airship.Id],
                input,
                airship.Transform,
                airship.MaxForwardSpeed,
                airship.MaxBackwardSpeed,
                airship.AccelerationRate,
                airship.DecelerationRate,
                deltaTime);

            // 2. Calculate Angular Velocity
            Vector3 angularVelocity = _processor.CalculateAngularVelocity(
                input,
                airship.Transform,
                airship.TurnSpeed,
                airship.PitchSpeed);

            // 3. Apply to view
            airship.ApplyMovement(_linearVelocities[airship.Id], angularVelocity);

            // 4. Update Camera Rotation if the Airship has an orbital camera attached
            if (isCaptured && airship is TinCan.Features.FreeCamera.IHasOrbitalCamera hasCamera && hasCamera.Look != null)
            {
                // The Airship controller view handles the base rotation interpolation,
                // but we need to ensure the camera script knows its pivot has moved.
                // Because _isRotationRelative is set, we don't need to manually add YawDelta.
            }
        }
    }
}
