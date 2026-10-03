using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Networking;
using System.Collections.Generic;
using System;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// Application Layer, server: simulates airship movement. Each tick the ship's input comes from its pilot
    /// (<see cref="IAirshipPilotInput"/>, the first source with an answer; the helm station today) and is kept on the
    /// ship as its current input, which other server systems read (fuel burns with the throttle).
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
        private readonly IReadOnlyList<IAirshipPilotInput> _pilots;
        private readonly Dictionary<Guid, MovementState> _states = new();

        public AirshipMovementUseCase(
            IInputReader input,
            INetworkService networkService,
            IActorRegistry registry,
            ITimeService timeService,
            AirshipMovementProcessor processor,
            IReadOnlyList<IAirshipPilotInput> pilots)
            : base(input, networkService, registry, timeService)
        {
            _processor = processor;
            _pilots = pilots;
        }

        // Nobody steers a ship from their own peer: its pilot's input reaches the server through the pilot source.
        protected override AirshipInputState GatherLocalInput(IAirshipView airship) => default;

        protected override void ProcessSimulation(IAirshipView airship, AirshipInputState _, bool isCaptured)
        {
            var input = PilotInput(airship);
            airship.InputState = input;

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

        private AirshipInputState PilotInput(IAirshipView airship)
        {
            foreach (var pilot in _pilots)
            {
                if (pilot.TryGetInput(airship, out var input)) return input;
            }
            return default;
        }
    }
}
