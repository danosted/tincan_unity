#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    public enum CloudTopology
    {
        Flat,
        Rolling,
        Layered
    }

    [CreateAssetMenu(fileName = "CloudBoundaryConfig", menuName = "TinCan/Environment/Cloud Boundary Config")]
    public class CloudBoundaryConfig : ScriptableObject
    {
        [SerializeField] private CloudTopology _topology = CloudTopology.Flat;
        [SerializeField] private int _worldSeed = 1;
        [SerializeField] private float _baseAltitude;
        [SerializeField, Min(0f)] private float _heightAmplitude = 20f;
        [SerializeField, Min(0.0001f)] private float _heightFrequency = 0.001f;
        [SerializeField, Min(0f)] private float _emergencyDepth = 5f;
        [SerializeField, Min(0f)] private float _recoveryMargin = 2f;
        [SerializeField, Min(0.1f)] private float _emergencyDuration = 15f;
        [SerializeField, Min(0f)] private float _characterResetDepth = 10f;
        [SerializeField] private Vector3 _fallbackRespawnOffset = new(0f, 3f, 0f);

        public CloudTopology Topology => _topology;
        public int WorldSeed => _worldSeed;
        public float BaseAltitude => _baseAltitude;
        public float HeightAmplitude => _heightAmplitude;
        public float HeightFrequency => _heightFrequency;
        public float EmergencyDepth => _emergencyDepth;
        public float RecoveryMargin => _recoveryMargin;
        public float EmergencyDuration => _emergencyDuration;
        public float CharacterResetDepth => _characterResetDepth;
        public Vector3 FallbackRespawnOffset => _fallbackRespawnOffset;
    }
}
