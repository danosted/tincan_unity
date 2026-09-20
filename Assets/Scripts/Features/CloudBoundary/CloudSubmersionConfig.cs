#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    [CreateAssetMenu(fileName = "CloudSubmersionConfig", menuName = "TinCan/Environment/Cloud Submersion Config")]
    public class CloudSubmersionConfig : ScriptableObject
    {
        [Tooltip("Vertical distance below the cloud surface over which submersion ramps from 0 to 1.")]
        [SerializeField, Min(1f)] private float _transitionDepth = 25f;

        [Tooltip("Directional light intensity multiplier at full submersion (1 = unaffected).")]
        [SerializeField, Range(0f, 1f)] private float _minLightMultiplier = 0.18f;

        [Tooltip("Multiplier applied to submersion to drive vapour VFX intensity; can exceed 1 to saturate early.")]
        [SerializeField, Min(0f)] private float _vapourGain = 1.25f;

        [Tooltip("Multiplier applied to submersion to drive audio muffling; can exceed 1 to saturate early.")]
        [SerializeField, Min(0f)] private float _audioGain = 1.5f;

        [Tooltip("Fog density blended in at full submersion, on top of the scene's own fog.")]
        [SerializeField, Min(0f)] private float _whiteoutFogDensity = 0.08f;

        [Tooltip("Fog color blended in at full submersion.")]
        [SerializeField] private Color _whiteoutFogColor = new(0.85f, 0.87f, 0.9f, 1f);

        public float TransitionDepth => _transitionDepth;
        public float MinLightMultiplier => _minLightMultiplier;
        public float VapourGain => _vapourGain;
        public float AudioGain => _audioGain;
        public float WhiteoutFogDensity => _whiteoutFogDensity;
        public Color WhiteoutFogColor => _whiteoutFogColor;
    }
}
