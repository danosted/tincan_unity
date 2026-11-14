#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
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

        [Header("Swell (topology Swell; the wavelength is 1 / height frequency)")]
        [Tooltip("How fast the swell rolls, in metres per second.")]
        [SerializeField, Min(0f)] private float _swellSpeed = 3f;

        [Header("Holes (plan .docs/plans/cloud-sea.md)")]
        [Tooltip("How far the surface drops inside an open hole. Bottomless for now: whoever falls that far is caught by the character reset depth below it.")]
        [SerializeField, Min(0f)] private float _holeDepth = 400f;
        [Tooltip("The width of a hole's rim, where the surface rolls down into it, as a fraction of the hole's radius.")]
        [SerializeField, Range(0.05f, 1f)] private float _holeRim = 0.35f;

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
        public float SwellSpeed => _swellSpeed;
        public float HoleDepth => _holeDepth;
        public float HoleRim => _holeRim;

        /// <summary>The waves of this surface; none unless the topology is <see cref="CloudTopology.Swell"/>.</summary>
        public CloudSwell Swell => _topology == CloudTopology.Swell
            ? new CloudSwell(_heightAmplitude, 1f / _heightFrequency, _swellSpeed, CloudSwell.OffsetOf(_worldSeed))
            : CloudSwell.None;
    }
}
