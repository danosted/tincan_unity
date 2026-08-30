#nullable enable
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    public readonly struct GasPocketVolumeDefinition
    {
        public Vector3 Center { get; }
        public float Radius { get; }
        public float DamagePerSecond { get; }

        public GasPocketVolumeDefinition(Vector3 center, float radius, float damagePerSecond)
        {
            Center = center;
            Radius = Mathf.Max(0.01f, radius);
            DamagePerSecond = Mathf.Max(0f, damagePerSecond);
        }

        public bool Contains(Vector3 point)
        {
            float distanceSquared = (point - Center).sqrMagnitude;
            return distanceSquared <= Radius * Radius;
        }
    }
}
