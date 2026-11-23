#nullable enable
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>Pure crank rules: what one turn adds to the tank.</summary>
    public static class CrankProcessor
    {
        /// <summary>
        /// The litres one turn adds: <paramref name="perTurn"/>, but never past the crank's reserve
        /// (<paramref name="maxLevel"/>, itself never past the tank's capacity). 0 once the tank is at the reserve.
        /// </summary>
        public static float Charge(float level, float capacity, float maxLevel, float perTurn)
        {
            float cap = Mathf.Min(maxLevel, capacity);
            return Mathf.Clamp(cap - level, 0f, Mathf.Max(0f, perTurn));
        }
    }
}
