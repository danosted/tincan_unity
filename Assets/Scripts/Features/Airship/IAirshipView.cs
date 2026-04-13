using TinCan.Core.Domain;
using TinCan.Features.Possession;
using UnityEngine;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Domain Layer: Interface for an airship that can be simulated and possessed.
    /// Also acts as a moving ground for actors standing on it.
    /// </summary>
    public interface IAirshipView : ISimulatedActor<AirshipInputState>, IPossessable, IMovingGround
    {
        Transform Transform { get; }

        // Configuration
        float MaxForwardSpeed { get; }
        float MaxBackwardSpeed { get; }
        float AccelerationRate { get; }
        float DecelerationRate { get; }
        float TurnSpeed { get; }
        float PitchSpeed { get; }

        /// <summary>
        /// Apply the calculated physical velocities to the view.
        /// </summary>
        void ApplyMovement(Vector3 velocity, Vector3 angularVelocity);
    }
}
