#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    [CreateAssetMenu(fileName = "CloudVisualProfile", menuName = "TinCan/Environment/Cloud Visual Profile")]
    public class CloudVisualProfile : ScriptableObject
    {
        [Header("Cloud Sea")]
        [SerializeField] private Material? _surfaceMaterial;
        [SerializeField, Min(100f)] private float _surfaceSize = 1200f;
        [SerializeField, Range(8, 128)] private int _surfaceResolution = 64;
        [SerializeField, Min(1f)] private float _surfaceRecenterDistance = 20f;

        [Header("Lower Cloud Bank")]
        [SerializeField, Min(20f)] private float _bankCellSize = 80f;
        [SerializeField, Range(1, 8)] private int _bankCellRadius = 5;
        [SerializeField, Range(1, 12)] private int _bankParticlesPerCell = 6;
        [SerializeField] private Vector2 _bankAltitudeOffset = new(-10f, 14f);
        [SerializeField] private Vector2 _bankParticleSize = new(28f, 52f);

        [Header("Cloud Cells")]
        [SerializeField] private Material? _puffMaterial;
        [SerializeField, Min(20f)] private float _cellSize = 120f;
        [SerializeField, Range(1, 6)] private int _cellRadius = 3;
        [SerializeField, Range(0, 8)] private int _clustersPerCell = 2;
        [SerializeField, Range(1, 8)] private int _puffsPerCluster = 5;
        [SerializeField] private Vector2 _altitudeAboveSurface = new(12f, 90f);
        [SerializeField] private Vector2 _clusterScale = new(18f, 42f);

        public Material? SurfaceMaterial => _surfaceMaterial;
        public float SurfaceSize => _surfaceSize;
        public int SurfaceResolution => _surfaceResolution;
        public float SurfaceRecenterDistance => _surfaceRecenterDistance;
        public float BankCellSize => _bankCellSize;
        public int BankCellRadius => _bankCellRadius;
        public int BankParticlesPerCell => _bankParticlesPerCell;
        public Vector2 BankAltitudeOffset => _bankAltitudeOffset;
        public Vector2 BankParticleSize => _bankParticleSize;
        public Material? PuffMaterial => _puffMaterial;
        public float CellSize => _cellSize;
        public int CellRadius => _cellRadius;
        public int ClustersPerCell => _clustersPerCell;
        public int PuffsPerCluster => _puffsPerCluster;
        public Vector2 AltitudeAboveSurface => _altitudeAboveSurface;
        public Vector2 ClusterScale => _clusterScale;
    }
}
