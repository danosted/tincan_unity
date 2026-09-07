#nullable enable
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>Plain-data spawn offsets so the processor stays free of ScriptableObjects.</summary>
    public readonly struct FlyingCanSpawnParameters
    {
        public readonly float LateralMin;
        public readonly float LateralMax;
        public readonly float HeightMin;
        public readonly float HeightMax;
        public readonly float DepthSpread;

        public FlyingCanSpawnParameters(float lateralMin, float lateralMax, float heightMin, float heightMax, float depthSpread)
        {
            LateralMin = lateralMin;
            LateralMax = lateralMax;
            HeightMin = heightMin;
            HeightMax = heightMax;
            DepthSpread = depthSpread;
        }
    }

    /// <summary>Domain Layer: spatial cadence and placement for stationary world pickups.</summary>
    public class FlyingCanWaveProcessor
    {
        public bool ShouldSpawn(Vector3 previousPosition, Vector3 position, float spacing) =>
            (position - previousPosition).sqrMagnitude >= Mathf.Pow(Mathf.Max(1f, spacing), 2f);

        public Quaternion TravelRotation(Vector3 displacement, Quaternion shipRotation)
        {
            if (displacement.sqrMagnitude < 0.0001f) return shipRotation;
            Vector3 forward = displacement.normalized;
            Vector3 up = shipRotation * Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.99f) up = shipRotation * Vector3.forward;
            return Quaternion.LookRotation(forward, up);
        }

        public Vector3 ComputeSpawn(
            Vector3 shipPosition, Quaternion rotation, float aheadDistance,
            float lateral01, float height01, float depth01, float side, FlyingCanSpawnParameters parameters)
        {
            float lateral = Mathf.Lerp(parameters.LateralMin, parameters.LateralMax, Mathf.Clamp01(lateral01)) * (side < 0f ? -1f : 1f);
            float height = Mathf.Lerp(parameters.HeightMin, parameters.HeightMax, Mathf.Clamp01(height01));
            float depth = Mathf.Lerp(-parameters.DepthSpread, parameters.DepthSpread, Mathf.Clamp01(depth01));
            return shipPosition + rotation * new Vector3(lateral, height, aheadDistance + depth);
        }

        public bool IsClearOfShip(Vector3 position, Vector3 shipPosition, float minimumDistance) =>
            (position - shipPosition).sqrMagnitude >= Mathf.Pow(Mathf.Max(0f, minimumDistance), 2f);
    }
}
