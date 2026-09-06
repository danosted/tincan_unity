#nullable enable
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Domain Layer: pure fuel arithmetic. No Unity objects, no state.
    /// </summary>
    public class FuelConsumptionProcessor
    {
        private const float ThrottleDeadZone = 0.001f;

        public float ComputeDrain(float throttle, bool isBoosting, float drainPerSecondAtFullThrottle, float boostMultiplier, float deltaTime)
        {
            float magnitude = Mathf.Abs(throttle);
            if (magnitude <= ThrottleDeadZone || deltaTime <= 0f) return 0f;

            float multiplier = isBoosting ? Mathf.Max(1f, boostMultiplier) : 1f;
            return magnitude * drainPerSecondAtFullThrottle * multiplier * deltaTime;
        }

        public float ClampLevel(float level, float capacity) => Mathf.Clamp(level, 0f, Mathf.Max(0f, capacity));

        public bool IsDriven(bool hasPossessor, float throttle) => hasPossessor && Mathf.Abs(throttle) > ThrottleDeadZone;
    }
}
