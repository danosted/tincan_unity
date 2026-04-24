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
        private class MovementState
        {
            public float CurrentSpeed;
            public Vector3 CurrentVelocity;
            public Vector3 CurrentAngularVelocity;
        }

        private readonly AirshipMovementProcessor _processor;
        private readonly Dictionary<Guid, MovementState> _states = new();

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

            var input = new AirshipInputState
            {
                Throttle = InputService.GetAxis(ActionNames.MoveForward, ActionNames.MoveBackward),
                Yaw = InputService.GetAxis(ActionNames.MoveRight, ActionNames.MoveLeft),
                Pitch = pitch
            };

            if (input.Throttle != 0 || input.Yaw != 0 || input.Pitch != 0)
            {
                Debug.Log($"[AirshipMovementUseCase] Gathered Input for {airship.Id}: T:{input.Throttle}, Y:{input.Yaw}, P:{input.Pitch}");
            }

            return input;
        }

        protected override void ProcessSimulation(IAirshipView airship, AirshipInputState input, bool isCaptured)
        {
            // If controls are disabled (unpossessed), we treat input as zeroed
            // but we do NOT return early so that momentum/drift can continue to simulate.
            if (airship.IsControlsEnabled == false)
            {
                input = new AirshipInputState();
            }

            if (!_states.ContainsKey(airship.Id))
                _states[airship.Id] = new MovementState();

            var state = _states[airship.Id];
            float deltaTime = TimeService.DeltaTime;

            // 1. Calculate Linear Speed
            state.CurrentSpeed = _processor.CalculateLinearSpeed(
                state.CurrentSpeed,
                input,
                airship.MaxForwardSpeed,
                airship.MaxBackwardSpeed,
                airship.AccelerationRate,
                airship.DecelerationRate,
                deltaTime);

            // 2. Calculate Drift Velocity
            state.CurrentVelocity = _processor.CalculateVelocityWithDrift(
                state.CurrentVelocity,
                airship.Transform.forward,
                state.CurrentSpeed,
                airship.VelocityBlendRate,
                deltaTime);

            // 3. Calculate Angular Velocity with Momentum and Banking
            state.CurrentAngularVelocity = _processor.CalculateAngularVelocity(
                state.CurrentAngularVelocity,
                input,
                state.CurrentSpeed,
                airship.MaxForwardSpeed,
                airship.Transform.rotation.eulerAngles.z,
                airship.TurnSpeed,
                airship.PitchSpeed,
                airship.AngularAcceleration,
                airship.AngularDeceleration,
                airship.MaxBankAngle,
                airship.BankSpeed,
                deltaTime);

            // 4. Apply to view
            airship.ApplyMovement(state.CurrentVelocity, state.CurrentAngularVelocity);
        }
    }
}
