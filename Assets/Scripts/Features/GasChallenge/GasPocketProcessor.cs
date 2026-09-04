#nullable enable
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    public static class GasPocketProcessor
    {
        public static GasPocketResult EvaluateAirship(
            GasPocketResult previousState,
            Vector3 airshipPosition,
            GasPocketVolumeDefinition pocket,
            float deltaTime)
        {
            bool isInGas = pocket.Contains(airshipPosition);
            float damageThisTick = isInGas ? pocket.DamagePerSecond * Mathf.Max(0f, deltaTime) : 0f;
            float warningLevel = isInGas ? 1f : 0f;

            if (!previousState.IsInGas && !isInGas)
            {
                damageThisTick = 0f;
            }

            return new GasPocketResult(isInGas, damageThisTick, warningLevel);
        }

        public static GasPocketResult EvaluateAirship(
            GasPocketResult previousState,
            Collider shipCollider,
            GasPocketVolume pocket,
            float deltaTime)
        {
            bool isInGas = pocket != null && pocket.ContainsCollider(shipCollider);
            float damageThisTick = isInGas ? pocket.DamagePerSecond * Mathf.Max(0f, deltaTime) : 0f;
            float warningLevel = isInGas ? 1f : 0f;

            if (!previousState.IsInGas && !isInGas)
            {
                damageThisTick = 0f;
            }

            return new GasPocketResult(isInGas, damageThisTick, warningLevel);
        }
    }
}
