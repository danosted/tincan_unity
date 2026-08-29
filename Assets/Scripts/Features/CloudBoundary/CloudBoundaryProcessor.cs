#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    public readonly struct CloudEmergencyState
    {
        public bool IsActive { get; }
        public bool HasExpired { get; }
        public float RemainingTime { get; }

        public CloudEmergencyState(bool isActive, bool hasExpired, float remainingTime)
        {
            IsActive = isActive;
            HasExpired = hasExpired;
            RemainingTime = remainingTime;
        }
    }

    public class CloudBoundaryProcessor
    {
        public CloudEmergencyState EvaluateAirship(
            CloudEmergencyState currentState,
            float altitude,
            float surfaceHeight,
            float emergencyDepth,
            float recoveryMargin,
            float emergencyDuration,
            float deltaTime)
        {
            if (currentState.HasExpired)
            {
                return currentState;
            }

            if (currentState.IsActive && altitude >= surfaceHeight + recoveryMargin)
            {
                return new CloudEmergencyState(false, false, emergencyDuration);
            }

            if (!currentState.IsActive)
            {
                return altitude <= surfaceHeight - emergencyDepth
                    ? new CloudEmergencyState(true, false, emergencyDuration)
                    : new CloudEmergencyState(false, false, emergencyDuration);
            }

            float remainingTime = Mathf.Max(0f, currentState.RemainingTime - Mathf.Max(0f, deltaTime));
            return new CloudEmergencyState(true, remainingTime <= 0f, remainingTime);
        }
    }
}
