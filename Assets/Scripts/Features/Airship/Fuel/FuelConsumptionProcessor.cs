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

        /// <summary>Fuel lost to leaks this tick; leaks drain whether or not the ship is driven. Never negative.</summary>
        public float ComputeLeak(float leakRatePerSecond, float deltaTime) => Mathf.Max(0f, leakRatePerSecond) * Mathf.Max(0f, deltaTime);

        /// <summary>The ship burns fuel while its pilot holds the throttle past the dead zone; with nobody at the helm its input is zero.</summary>
        public bool IsDriven(float throttle) => Mathf.Abs(throttle) > ThrottleDeadZone;
    }
}
