#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    [CreateAssetMenu(fileName = "CloudVisualProfile", menuName = "TinCan/Environment/Cloud Visual Profile")]
    public class CloudVisualProfile : ScriptableObject
    {
        [Header("Surface Bridge")]
        [SerializeField, Range(16, 256)] private int _heightMapResolution = 64;
        [SerializeField, Min(500f)] private float _heightMapWorldSize = 4096f;
        [SerializeField, Min(1f)] private float _heightMapRecenterDistance = 64f;

        [Header("Volumetric Layer")]
        [SerializeField, Min(10f)] private float _layerThickness = 240f;
        [SerializeField, Min(0f)] private float _depthBelowSurface = 220f;
        [SerializeField, Min(1000f)] private float _shellRadius = 100000f;
        [SerializeField, Min(100f)] private float _maxRenderDistance = 4000f;
        [SerializeField, Range(8, 96)] private int _stepCount = 48;

        [Header("Cloud Shape")]
        [SerializeField, Range(0f, 1f)] private float _coverage = 0.62f;
        [SerializeField, Min(0f)] private float _density = 1.15f;
        [SerializeField, Min(0.0001f)] private float _noiseScale = 0.0025f;
        [SerializeField] private Vector2 _wind = new(8f, 3f);

        [Header("Lighting")]
        [SerializeField] private Color _baseColor = new(0.95f, 0.96f, 1f, 1f);
        [SerializeField] private Color _sunColor = new(1f, 0.97f, 0.9f, 1f);
        [SerializeField, Range(0f, 4f)] private float _lightAbsorption = 1f;
        [SerializeField, Range(0f, 0.95f)] private float _scatterEccentricity = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _powderStrength = 0.6f;
        [SerializeField, Range(0f, 2f)] private float _ambientStrength = 1f;
        [SerializeField, Range(0.1f, 1f)] private float _distanceFadeStart = 0.55f;

        public int HeightMapResolution => _heightMapResolution;
        public float HeightMapWorldSize => _heightMapWorldSize;
        public float HeightMapRecenterDistance => _heightMapRecenterDistance;
        public float LayerThickness => _layerThickness;
        public float DepthBelowSurface => _depthBelowSurface;
        public float ShellRadius => _shellRadius;
        public float MaxRenderDistance => _maxRenderDistance;
        public int StepCount => _stepCount;
        public float Coverage => _coverage;
        public float Density => _density;
        public float NoiseScale => _noiseScale;
        public Vector2 Wind => _wind;
        public Color BaseColor => _baseColor;
        public Color SunColor => _sunColor;
        public float LightAbsorption => _lightAbsorption;
        public float ScatterEccentricity => _scatterEccentricity;
        public float PowderStrength => _powderStrength;
        public float AmbientStrength => _ambientStrength;
        public float DistanceFadeStart => _distanceFadeStart;
    }
}
