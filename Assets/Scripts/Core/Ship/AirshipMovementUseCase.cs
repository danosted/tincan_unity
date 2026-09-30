using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Networking;
using System.Collections.Generic;
using System;

namespace TinCan.Core.Ship
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

        private readonly AirshipInputContext _controls;
        private readonly AirshipMovementProcessor _processor;
        private readonly Dictionary<Guid, MovementState> _states = new();

        public AirshipMovementUseCase(
            IInputReader input,
            AirshipInputContext controls,
            INetworkService networkService,
            IActorRegistry registry,
            ITimeService timeService,
            AirshipMovementProcessor processor)
            : base(input, networkService, registry, timeService)
        {
            _controls = controls;
            _processor = processor;
        }

        protected override AirshipInputState GatherLocalInput(IAirshipView airship) => new()
        {
            Throttle = Input.ReadAxis(_controls.Throttle),
            Yaw = Input.ReadAxis(_controls.Yaw),
            Pitch = Input.ReadAxis(_controls.Pitch)
        };

        protected override void ProcessSimulation(IAirshipView airship, AirshipInputState input, bool isCaptured)
        {
            // Presentation control state is local-only. The server accepts steering
            // solely while an authoritative possessor is assigned.
            if (!airship.PossessorId.HasValue)
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
            airship.Simulate(deltaTime);
        }
    }
}
