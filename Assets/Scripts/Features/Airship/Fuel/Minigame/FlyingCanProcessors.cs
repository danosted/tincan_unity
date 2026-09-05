#nullable enable
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>Plain-data copy of the spawn tunables so the processor stays free of ScriptableObjects.</summary>
    public readonly struct FlyingCanSpawnParameters
    {
        public readonly float AheadDistance;
        public readonly float LateralMin;
        public readonly float LateralMax;
        public readonly float HeightMin;
        public readonly float HeightMax;
        public readonly float CanSpeed;

        public FlyingCanSpawnParameters(float aheadDistance, float lateralMin, float lateralMax, float heightMin, float heightMax, float canSpeed)
        {
            AheadDistance = aheadDistance;
            LateralMin = lateralMin;
            LateralMax = lateralMax;
            HeightMin = heightMin;
            HeightMax = heightMax;
            CanSpeed = canSpeed;
        }
    }

    /// <summary>
    /// Domain Layer: decides when a can spawns and where. Cans start ahead of the ship, offset to one side just
    /// outside the rail, and fly straight back along the ship's forward axis in world space.
    /// </summary>
    public class FlyingCanWaveProcessor
    {
        public bool ShouldSpawn(float sinceLastSpawn, int alive, float spawnInterval, int maxAlive) =>
            alive < maxAlive && sinceLastSpawn >= spawnInterval;

        public (Vector3 Position, Vector3 Velocity) ComputeSpawn(
            Vector3 shipPosition,
            Quaternion shipRotation,
            float lateral01,
            float height01,
            float side,
            FlyingCanSpawnParameters parameters)
        {
            float lateral = Mathf.Lerp(parameters.LateralMin, parameters.LateralMax, Mathf.Clamp01(lateral01)) * (side < 0f ? -1f : 1f);
            float height = Mathf.Lerp(parameters.HeightMin, parameters.HeightMax, Mathf.Clamp01(height01));

            Vector3 local = new(lateral, height, parameters.AheadDistance);
            Vector3 position = shipPosition + shipRotation * local;
            Vector3 velocity = -(shipRotation * Vector3.forward) * parameters.CanSpeed;
            return (position, velocity);
        }
    }

    /// <summary>Domain Layer: straight-line motion per tick.</summary>
    public class FlyingCanMotionProcessor
    {
        public Vector3 Step(Vector3 position, Vector3 velocity, float deltaTime) => position + velocity * Mathf.Max(0f, deltaTime);

        public bool IsExpired(float spawnTime, float now, float lifetime) => lifetime > 0f && now - spawnTime >= lifetime;
    }
}
