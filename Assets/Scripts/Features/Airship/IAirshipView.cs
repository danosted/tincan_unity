using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Domain Layer: Interface for an airship that can be simulated and possessed.
    /// Also acts as a moving ground for actors standing on it.
    /// </summary>
    public interface IAirshipView : ISimulatedActor<AirshipInputState>, IPossessable, IPointVelocityMovingGround, IControllable
    {
        Transform Transform { get; }

        // Configuration
        float MaxForwardSpeed { get; }
        float MaxBackwardSpeed { get; }
        float AccelerationRate { get; }
        float DecelerationRate { get; }
        float AngularAcceleration { get; }
        float AngularDeceleration { get; }
        float VelocityBlendRate { get; }
        float TurnSpeed { get; }
        float PitchSpeed { get; }
        float MaxBankAngle { get; }
        float BankSpeed { get; }

        /// <summary>
        /// Apply the calculated physical velocities to the view.
        /// </summary>
        void ApplyMovement(Vector3 velocity, Vector3 angularVelocity);

        /// <summary>
        /// Advances the physical airship pose for one simulation tick.
        /// </summary>
        void Simulate(float deltaTime);
    }
}
