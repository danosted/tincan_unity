#nullable enable
using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    public static class HumanoidPredictionReconciliation
    {
        public static float CalculateBlendFactor(float smoothing, float deltaTime)
        {
            if (deltaTime <= 0f || smoothing <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-smoothing * deltaTime);
        }

        public static bool TryComputePositionError(Vector3 authoritativePosition, Vector3 predictedPosition, out Vector3 correction)
        {
            correction = authoritativePosition - predictedPosition;
            return correction.sqrMagnitude > 0f;
        }
    }
}
