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
    /// Something solid can push the ship out of itself (<see cref="IAirshipCollisionResponse"/>).
    /// Features can add forces to the flight (<see cref="IAirshipFlightForces"/>: momentum, a vertical and a sideways push, a nose drop);
    /// with none, the ship flies on its engine alone. A feature can hold the ship still (<see cref="IAirshipHold"/>: the
    /// anchor), which comes before all of it. With no pilot the ship winds down to neutral: the nose levels quickly and
    /// the forces' momentum fades at the deceleration rate, so a nose left pitched down does not dive on.
    /// </summary>
    public class AirshipMovementUseCase : SimulationUseCase<IAirshipView, AirshipInputState>, IAirshipCollisionResponse
    {
        private class MovementState
        {
            public float CurrentSpeed;
            public Vector3 CurrentVelocity;
            public Vector3 CurrentAngularVelocity;
            /// <summary>Speed along the nose beyond the engine's, from <see cref="IAirshipFlightForces"/>.</summary>
            public float Momentum;
        }

        /// <summary>How much of the velocity into a surface comes back out of it when the ship is pushed.</summary>
        public const float Bounce = 0.3f;

        /// <summary>With nobody piloting, how fast the nose eases back to level, per second of tilt (about a second to
        /// level, capped at the ship's pitch speed). A pilot gets the ship's own, gentler pitch level speed.</summary>
        public const float UnpilotedPitchLevelSpeed = 1f;

        private readonly AirshipMovementProcessor _processor;
        private readonly IReadOnlyList<IAirshipPilotInput> _pilots;
        private readonly IReadOnlyList<IAirshipFlightForces> _forces;
        private readonly IReadOnlyList<IAirshipHold> _holds;
        private readonly Dictionary<Guid, MovementState> _states = new();

        public AirshipMovementUseCase(
            IInputReader input,
            INetworkService networkService,
            IActorRegistry registry,
            ITimeService timeService,
            AirshipMovementProcessor processor,
            IReadOnlyList<IAirshipPilotInput> pilots,
            IReadOnlyList<IAirshipFlightForces> forces,
            IReadOnlyList<IAirshipHold> holds)
            : base(input, networkService, registry, timeService)
        {
            _processor = processor;
            _pilots = pilots;
            _forces = forces;
            _holds = holds;
        }

        // Nobody steers a ship from their own peer: its pilot's input reaches the server through the pilot source.
        protected override AirshipInputState GatherLocalInput(IAirshipView airship) => default;

        protected override void ProcessSimulation(IAirshipView airship, AirshipInputState _, bool isCaptured)
        {
            bool piloted = TryGetPilotInput(airship, out var input);
            airship.InputState = input;

            if (!_states.ContainsKey(airship.Id))
                _states[airship.Id] = new MovementState();

            var state = _states[airship.Id];
            float deltaTime = TimeService.DeltaTime;

            // 0. Held (anchored): still where it is, and from rest when let go (no stored speed or momentum).
            if (IsHeld(airship))
            {
                state.CurrentSpeed = 0f;
                state.Momentum = 0f;
                state.CurrentVelocity = Vector3.zero;
                state.CurrentAngularVelocity = Vector3.zero;
                airship.ApplyMovement(Vector3.zero, Vector3.zero);
                airship.Simulate(deltaTime);
                return;
            }

            // 1. Calculate Linear Speed
            state.CurrentSpeed = _processor.CalculateLinearSpeed(
                state.CurrentSpeed,
                input,
                airship.MaxForwardSpeed,
                airship.MaxBackwardSpeed,
                airship.AccelerationRate,
                airship.DecelerationRate,
                deltaTime);

            // 2. The features' forces: momentum on top of the engine, a vertical and a sideways push, a nose drop, where
            // level is.
            float vertical = 0f, noseDrop = 0f, level = 0f;
            Vector3 horizontal = Vector3.zero;
            foreach (var source in _forces)
            {
                var force = source.Evaluate(airship, state.CurrentSpeed, state.Momentum, deltaTime);
                state.Momentum += force.MomentumChange;
                vertical += force.Vertical;
                horizontal += force.Horizontal;
                noseDrop += force.NoseDrop;
                level += force.Level;
            }

            // The ship's own say in how much the world pitches it (lift and momentum stay whole).
            float influence = Mathf.Clamp01(airship.ExternalPitchInfluence);
            noseDrop *= influence;
            level *= influence;

            // Unpiloted, the ship winds down to neutral: the speed the forces gave it fades like the engine's.
            if (!piloted)
                state.Momentum = Mathf.MoveTowards(state.Momentum, 0f, airship.DecelerationRate * deltaTime);

            // 3. Calculate Drift Velocity (the vertical and sideways pushes blend in the same way: the lerp toward
            // forward + push).
            state.CurrentVelocity = _processor.CalculateVelocityWithDrift(
                state.CurrentVelocity,
                airship.Transform.forward,
                state.CurrentSpeed + state.Momentum,
                airship.VelocityBlendRate,
                deltaTime) + (Vector3.up * vertical + horizontal) * Mathf.Clamp01(airship.VelocityBlendRate * deltaTime);

            // 4. Calculate Angular Velocity with Momentum and Banking
            state.CurrentAngularVelocity = _processor.CalculateAngularVelocity(
                state.CurrentAngularVelocity,
                input,
                state.CurrentSpeed,
                airship.MaxForwardSpeed,
                airship.Transform.rotation.eulerAngles.z,
                -Mathf.Asin(Mathf.Clamp(airship.Transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg - level,
                airship.TurnSpeed,
                airship.PitchSpeed,
                airship.AngularAcceleration,
                airship.AngularDeceleration,
                airship.MaxBankAngle,
                airship.BankSpeed,
                piloted ? airship.PitchLevelSpeed : UnpilotedPitchLevelSpeed,
                deltaTime);

            // 5. Apply to view (the nose drop is applied, not kept: it never builds up in the pitch momentum).
            Vector3 angular = state.CurrentAngularVelocity;
            angular.x += noseDrop;
            airship.ApplyMovement(state.CurrentVelocity, angular);
            airship.Simulate(deltaTime);
        }

        public void Push(IAirshipView airship, Vector3 push)
        {
            if (!NetworkService.IsServer || push.sqrMagnitude < 1e-8f || airship.Transform == null) return;

            Vector3 normal = push.normalized;
            airship.Transform.position += push;
            if (!_states.TryGetValue(airship.Id, out var state)) return;

            float into = Vector3.Dot(state.CurrentVelocity, normal);
            if (into < 0f) state.CurrentVelocity -= normal * (into * (1f + Bounce));

            // Head on, the engine stops pushing into the rock; along it, it keeps most of its way.
            float facing = Vector3.Dot(airship.Transform.forward, normal);
            if (facing < 0f)
            {
                state.CurrentSpeed *= 1f + facing;
                state.Momentum *= 1f + facing;
            }
        }

        private bool IsHeld(IAirshipView airship)
        {
            foreach (var hold in _holds)
            {
                if (hold.Holds(airship)) return true;
            }
            return false;
        }

        private bool TryGetPilotInput(IAirshipView airship, out AirshipInputState input)
        {
            foreach (var pilot in _pilots)
            {
                if (pilot.TryGetInput(airship, out input)) return true;
            }
            input = default;
            return false;
        }
    }
}
