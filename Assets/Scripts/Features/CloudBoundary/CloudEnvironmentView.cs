#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace TinCan.Features.CloudBoundary
{
    public class CloudEnvironmentView : MonoBehaviour
    {
        private const float CameraSearchInterval = 0.5f;

        private readonly List<Matrix4x4> _puffMatrices = new();
        private readonly List<GameObject> _puffPool = new();
        private ICloudSurfaceQuery? _surfaceQuery;
        private CloudBoundaryConfig? _boundaryConfig;
        private CloudVisualProfile? _visualProfile;
        private GameObject? _surfaceObject;
        private Transform? _puffRoot;
        private ParticleSystem? _bankParticles;
        private Mesh? _surfaceMesh;
        private Mesh? _puffMesh;
        private Camera? _renderCamera;
        private float _nextCameraSearchTime;
        private Vector2 _surfaceCenter = new(float.PositiveInfinity, float.PositiveInfinity);
        private Vector2Int _bankCell = new(int.MinValue, int.MinValue);
        private Vector2Int _cloudCell = new(int.MinValue, int.MinValue);

        [Inject]
        public void Construct(
            ICloudSurfaceQuery surfaceQuery,
            CloudBoundaryConfig boundaryConfig,
            CloudVisualProfile visualProfile)
        {
            _surfaceQuery = surfaceQuery;
            _boundaryConfig = boundaryConfig;
            _visualProfile = visualProfile;
        }

        private void Start()
        {
            if (_surfaceQuery == null || _boundaryConfig == null || _visualProfile == null)
            {
                Debug.LogError("[CloudEnvironmentView] Cloud dependencies were not injected.");
                enabled = false;
                return;
            }

            CreateSurface();
            _puffMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            CreateBankParticles();
            var puffRootObject = new GameObject("Cloud Puffs");
            puffRootObject.transform.SetParent(transform, false);
            _puffRoot = puffRootObject.transform;
        }

        private void Update()
        {
            if (_surfaceQuery == null || _boundaryConfig == null || _visualProfile == null)
            {
                return;
            }

            Camera? camera = ResolveRenderCamera();
            if (camera == null)
            {
                return;
            }

            UpdateSurface(camera.transform.position);
            UpdateBankParticles(camera.transform.position);
            UpdateCloudCells(camera.transform.position);
        }

        private Camera? ResolveRenderCamera()
        {
            if (_renderCamera != null && _renderCamera.isActiveAndEnabled)
            {
                return _renderCamera;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.isActiveAndEnabled)
            {
                _renderCamera = mainCamera;
                return _renderCamera;
            }

            if (Time.unscaledTime < _nextCameraSearchTime)
            {
                return null;
            }

            _nextCameraSearchTime = Time.unscaledTime + CameraSearchInterval;
            foreach (Camera candidate in Camera.allCameras)
            {
                if (!candidate.isActiveAndEnabled ||
                    candidate.cameraType != CameraType.Game ||
                    candidate.targetTexture != null)
                {
                    continue;
                }

                _renderCamera = candidate;
                return _renderCamera;
            }

            return null;
        }

        private void OnDestroy()
        {
            if (_surfaceMesh != null)
            {
                Destroy(_surfaceMesh);
            }
        }

        private void CreateSurface()
        {
            if (_visualProfile?.SurfaceMaterial == null)
            {
                Debug.LogWarning("[CloudEnvironmentView] No cloud surface material is configured.");
                return;
            }

            _surfaceObject = new GameObject("Cloud Sea");
            _surfaceObject.transform.SetParent(transform, false);
            var meshFilter = _surfaceObject.AddComponent<MeshFilter>();
            var meshRenderer = _surfaceObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _visualProfile.SurfaceMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            _surfaceMesh = new Mesh { name = "Procedural Cloud Sea" };
            _surfaceMesh.MarkDynamic();
            meshFilter.sharedMesh = _surfaceMesh;
        }

        private void CreateBankParticles()
        {
            if (_visualProfile?.PuffMaterial == null || _puffMesh == null)
            {
                Debug.LogWarning("[CloudEnvironmentView] No material or mesh is configured for lower cloud particles.");
                return;
            }

            var bankObject = new GameObject("Lower Cloud Bank Particles");
            bankObject.transform.SetParent(transform, false);
            _bankParticles = bankObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = _bankParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = GetBankParticleCapacity();
            main.startSize3D = true;

            ParticleSystem.EmissionModule emission = _bankParticles.emission;
            emission.enabled = false;
            ParticleSystem.CollisionModule collision = _bankParticles.collision;
            collision.enabled = false;

            var particleRenderer = bankObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = _puffMesh;
            particleRenderer.sharedMaterial = _visualProfile.PuffMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
        }

        private void UpdateSurface(Vector3 cameraPosition)
        {
            if (_surfaceObject == null || _surfaceMesh == null || _surfaceQuery == null || _visualProfile == null)
            {
                return;
            }

            var nextCenter = new Vector2(cameraPosition.x, cameraPosition.z);
            if (Vector2.Distance(_surfaceCenter, nextCenter) < _visualProfile.SurfaceRecenterDistance)
            {
                return;
            }

            float snap = _visualProfile.SurfaceRecenterDistance;
            _surfaceCenter = new Vector2(
                Mathf.Round(nextCenter.x / snap) * snap,
                Mathf.Round(nextCenter.y / snap) * snap);
            _surfaceObject.transform.position = new Vector3(_surfaceCenter.x, 0f, _surfaceCenter.y);
            RebuildSurfaceMesh();
        }

        private void RebuildSurfaceMesh()
        {
            if (_surfaceMesh == null || _surfaceQuery == null || _visualProfile == null)
            {
                return;
            }

            int resolution = _visualProfile.SurfaceResolution;
            int rowSize = resolution + 1;
            var vertices = new Vector3[rowSize * rowSize];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[resolution * resolution * 6];
            float halfSize = _visualProfile.SurfaceSize * 0.5f;
            float step = _visualProfile.SurfaceSize / resolution;

            for (int z = 0; z < rowSize; z++)
            {
                for (int x = 0; x < rowSize; x++)
                {
                    int index = z * rowSize + x;
                    float localX = -halfSize + x * step;
                    float localZ = -halfSize + z * step;
                    float worldX = _surfaceCenter.x + localX;
                    float worldZ = _surfaceCenter.y + localZ;
                    vertices[index] = new Vector3(localX, _surfaceQuery.GetSurfaceHeight(worldX, worldZ), localZ);
                    uv[index] = new Vector2((float)x / resolution, (float)z / resolution);
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int bottomLeft = z * rowSize + x;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = bottomLeft + rowSize;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = bottomLeft + rowSize;
                    triangles[triangleIndex++] = bottomLeft + rowSize + 1;
                }
            }

            _surfaceMesh.Clear();
            _surfaceMesh.vertices = vertices;
            _surfaceMesh.uv = uv;
            _surfaceMesh.triangles = triangles;
            _surfaceMesh.RecalculateNormals();
            _surfaceMesh.RecalculateBounds();
        }

        private void UpdateBankParticles(Vector3 cameraPosition)
        {
            if (_bankParticles == null || _boundaryConfig == null || _surfaceQuery == null || _visualProfile == null)
            {
                return;
            }

            float cellSize = _visualProfile.BankCellSize;
            var nextCell = new Vector2Int(
                Mathf.FloorToInt(cameraPosition.x / cellSize),
                Mathf.FloorToInt(cameraPosition.z / cellSize));
            if (nextCell == _bankCell)
            {
                return;
            }

            _bankCell = nextCell;
            var particles = new ParticleSystem.Particle[GetBankParticleCapacity()];
            int particleIndex = 0;
            int radius = _visualProfile.BankCellRadius;
            for (int cellZ = nextCell.y - radius; cellZ <= nextCell.y + radius; cellZ++)
            {
                for (int cellX = nextCell.x - radius; cellX <= nextCell.x + radius; cellX++)
                {
                    for (int item = 0; item < _visualProfile.BankParticlesPerCell; item++)
                    {
                        uint seed = Hash(_boundaryConfig.WorldSeed ^ 0x5F3759DF, cellX, cellZ, item);
                        float worldX = (cellX + Next01(ref seed)) * cellSize;
                        float worldZ = (cellZ + Next01(ref seed)) * cellSize;
                        float surfaceHeight = _surfaceQuery.GetSurfaceHeight(worldX, worldZ);
                        float particleSize = Mathf.Lerp(
                            _visualProfile.BankParticleSize.x,
                            _visualProfile.BankParticleSize.y,
                            Next01(ref seed));
                        var particle = new ParticleSystem.Particle
                        {
                            position = new Vector3(
                                worldX,
                                surfaceHeight + Mathf.Lerp(
                                    _visualProfile.BankAltitudeOffset.x,
                                    _visualProfile.BankAltitudeOffset.y,
                                    Next01(ref seed)),
                                worldZ),
                            rotation = Next01(ref seed) * 360f,
                            startColor = Color.white,
                            remainingLifetime = float.MaxValue,
                            startLifetime = float.MaxValue
                        };
                        particle.startSize3D = new Vector3(
                            particleSize * Mathf.Lerp(0.8f, 1.2f, Next01(ref seed)),
                            particleSize * Mathf.Lerp(0.28f, 0.48f, Next01(ref seed)),
                            particleSize * Mathf.Lerp(0.65f, 1f, Next01(ref seed)));
                        particles[particleIndex++] = particle;
                    }
                }
            }

            _bankParticles.SetParticles(particles, particleIndex);
        }

        private int GetBankParticleCapacity()
        {
            if (_visualProfile == null)
            {
                return 0;
            }

            int diameter = _visualProfile.BankCellRadius * 2 + 1;
            return diameter * diameter * _visualProfile.BankParticlesPerCell;
        }

        private void UpdateCloudCells(Vector3 cameraPosition)
        {
            if (_boundaryConfig == null || _surfaceQuery == null || _visualProfile == null)
            {
                return;
            }

            float cellSize = _visualProfile.CellSize;
            var nextCell = new Vector2Int(
                Mathf.FloorToInt(cameraPosition.x / cellSize),
                Mathf.FloorToInt(cameraPosition.z / cellSize));
            if (nextCell == _cloudCell)
            {
                return;
            }

            _cloudCell = nextCell;
            _puffMatrices.Clear();
            int radius = _visualProfile.CellRadius;
            for (int cellZ = nextCell.y - radius; cellZ <= nextCell.y + radius; cellZ++)
            {
                for (int cellX = nextCell.x - radius; cellX <= nextCell.x + radius; cellX++)
                {
                    AddCellClouds(cellX, cellZ);
                }
            }

            ApplyPuffPool();
        }

        private void AddCellClouds(int cellX, int cellZ)
        {
            if (_boundaryConfig == null || _surfaceQuery == null || _visualProfile == null)
            {
                return;
            }

            for (int cluster = 0; cluster < _visualProfile.ClustersPerCell; cluster++)
            {
                uint seed = Hash(_boundaryConfig.WorldSeed, cellX, cellZ, cluster);
                float worldX = (cellX + Next01(ref seed)) * _visualProfile.CellSize;
                float worldZ = (cellZ + Next01(ref seed)) * _visualProfile.CellSize;
                float surfaceHeight = _surfaceQuery.GetSurfaceHeight(worldX, worldZ);
                float altitude = surfaceHeight + Mathf.Lerp(
                    _visualProfile.AltitudeAboveSurface.x,
                    _visualProfile.AltitudeAboveSurface.y,
                    Next01(ref seed));
                float clusterScale = Mathf.Lerp(
                    _visualProfile.ClusterScale.x,
                    _visualProfile.ClusterScale.y,
                    Next01(ref seed));

                for (int puff = 0; puff < _visualProfile.PuffsPerCluster; puff++)
                {
                    Vector3 offset = new(
                        (Next01(ref seed) - 0.5f) * clusterScale,
                        (Next01(ref seed) - 0.5f) * clusterScale * 0.25f,
                        (Next01(ref seed) - 0.5f) * clusterScale * 0.55f);
                    Vector3 scale = new(
                        clusterScale * Mathf.Lerp(0.35f, 0.7f, Next01(ref seed)),
                        clusterScale * Mathf.Lerp(0.16f, 0.3f, Next01(ref seed)),
                        clusterScale * Mathf.Lerp(0.3f, 0.6f, Next01(ref seed)));
                    _puffMatrices.Add(Matrix4x4.TRS(
                        new Vector3(worldX, altitude, worldZ) + offset,
                        Quaternion.Euler(0f, Next01(ref seed) * 360f, 0f),
                        scale));
                }
            }
        }

        private void ApplyPuffPool()
        {
            if (_puffMesh == null || _puffRoot == null || _visualProfile?.PuffMaterial == null)
            {
                return;
            }

            while (_puffPool.Count < _puffMatrices.Count)
            {
                var puffObject = new GameObject($"Cloud Puff {_puffPool.Count}");
                puffObject.transform.SetParent(_puffRoot, false);
                var meshFilter = puffObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = _puffMesh;
                var meshRenderer = puffObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = _visualProfile.PuffMaterial;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                _puffPool.Add(puffObject);
            }

            for (int index = 0; index < _puffPool.Count; index++)
            {
                GameObject puffObject = _puffPool[index];
                bool isActive = index < _puffMatrices.Count;
                puffObject.SetActive(isActive);
                if (!isActive)
                {
                    continue;
                }

                Matrix4x4 matrix = _puffMatrices[index];
                puffObject.transform.SetPositionAndRotation(matrix.GetPosition(), matrix.rotation);
                puffObject.transform.localScale = matrix.lossyScale;
            }
        }

        private static uint Hash(int worldSeed, int cellX, int cellZ, int item)
        {
            unchecked
            {
                uint hash = (uint)worldSeed ^ 2166136261u;
                hash = (hash ^ (uint)cellX) * 16777619u;
                hash = (hash ^ (uint)cellZ) * 16777619u;
                hash = (hash ^ (uint)item) * 16777619u;
                return hash;
            }
        }

        private static float Next01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) / 16777216f;
        }
    }
}
