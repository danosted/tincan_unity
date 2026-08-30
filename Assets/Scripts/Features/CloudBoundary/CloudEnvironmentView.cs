#nullable enable
using UnityEngine;
using VContainer;

namespace TinCan.Features.CloudBoundary
{
    public class CloudEnvironmentView : MonoBehaviour
    {
        private const float CameraSearchInterval = 0.5f;
        private static readonly int CloudEnabledId = Shader.PropertyToID("_CloudEnabled");
        private static readonly int CloudHeightMapId = Shader.PropertyToID("_CloudHeightMap");
        private static readonly int CloudHeightMapCenterId = Shader.PropertyToID("_CloudHeightMapCenter");
        private static readonly int CloudLayerParamsId = Shader.PropertyToID("_CloudLayerParams");
        private static readonly int CloudShapeParamsId = Shader.PropertyToID("_CloudShapeParams");
        private static readonly int CloudLightParamsId = Shader.PropertyToID("_CloudLightParams");
        private static readonly int CloudAmbientSkyId = Shader.PropertyToID("_CloudAmbientSky");
        private static readonly int CloudAmbientGroundId = Shader.PropertyToID("_CloudAmbientGround");
        private static readonly int CloudFadeStartId = Shader.PropertyToID("_CloudFadeStart");
        private static readonly int CloudWindId = Shader.PropertyToID("_CloudWind");
        private static readonly int CloudBaseColorId = Shader.PropertyToID("_CloudBaseColor");
        private static readonly int CloudSunColorId = Shader.PropertyToID("_CloudSunColor");
        private static readonly int CloudStepCountId = Shader.PropertyToID("_CloudStepCount");

        private ICloudSurfaceQuery? _surfaceQuery;
        private CloudBoundaryConfig? _boundaryConfig;
        private CloudVisualProfile? _visualProfile;
        private readonly Vector3[] _ambientDirections = { Vector3.up, Vector3.down };
        private readonly Color[] _ambientColors = new Color[2];
        private Texture2D? _heightMap;
        private float[]? _heightSamples;
        private Camera? _renderCamera;
        private float _nextCameraSearchTime;
        private Vector2 _heightMapCenter = new(float.PositiveInfinity, float.PositiveInfinity);

        public bool IsReady => _heightMap != null;
        public Texture2D? HeightMap => _heightMap;

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

            int resolution = _visualProfile.HeightMapResolution;
            _heightSamples = new float[resolution * resolution];
            _heightMap = new Texture2D(resolution, resolution, TextureFormat.RFloat, false, true)
            {
                name = "Cloud Surface Height Map",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Shader.SetGlobalTexture(CloudHeightMapId, _heightMap);
            Shader.SetGlobalFloat(CloudEnabledId, 1f);
            ApplyGlobalSettings();
        }

        private void Update()
        {
            if (_surfaceQuery == null || _visualProfile == null || _heightMap == null || _heightSamples == null)
            {
                return;
            }

            Camera? camera = ResolveRenderCamera();
            if (camera == null)
            {
                return;
            }

            UpdateHeightMap(camera.transform.position);
            ApplyGlobalSettings();
        }

        private void OnDestroy()
        {
            Shader.SetGlobalFloat(CloudEnabledId, 0f);
            if (_heightMap != null)
            {
                Destroy(_heightMap);
            }
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
                if (!candidate.isActiveAndEnabled || candidate.cameraType != CameraType.Game)
                {
                    continue;
                }

                _renderCamera = candidate;
                return _renderCamera;
            }

            return null;
        }

        private void UpdateHeightMap(Vector3 cameraPosition)
        {
            if (_surfaceQuery == null || _visualProfile == null || _heightMap == null || _heightSamples == null)
            {
                return;
            }

            float snap = _visualProfile.HeightMapRecenterDistance;
            var nextCenter = new Vector2(
                Mathf.Round(cameraPosition.x / snap) * snap,
                Mathf.Round(cameraPosition.z / snap) * snap);
            if (nextCenter == _heightMapCenter)
            {
                return;
            }

            _heightMapCenter = nextCenter;
            int resolution = _visualProfile.HeightMapResolution;
            float worldSize = _visualProfile.HeightMapWorldSize;
            float sampleSpacing = worldSize / (resolution - 1);
            float startX = nextCenter.x - worldSize * 0.5f;
            float startZ = nextCenter.y - worldSize * 0.5f;

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    _heightSamples[z * resolution + x] = _surfaceQuery.GetSurfaceHeight(
                        startX + x * sampleSpacing,
                        startZ + z * sampleSpacing);
                }
            }

            _heightMap.SetPixelData(_heightSamples, 0);
            _heightMap.Apply(false, false);
            Shader.SetGlobalVector(CloudHeightMapCenterId, new Vector4(
                nextCenter.x,
                nextCenter.y,
                worldSize,
                1f / worldSize));
        }

        private void ApplyGlobalSettings()
        {
            if (_visualProfile == null || _boundaryConfig == null)
            {
                return;
            }

            Shader.SetGlobalVector(CloudLayerParamsId, new Vector4(
                _visualProfile.LayerThickness,
                _visualProfile.DepthBelowSurface,
                _visualProfile.ShellRadius,
                _visualProfile.MaxRenderDistance));
            Shader.SetGlobalVector(CloudShapeParamsId, new Vector4(
                _visualProfile.Coverage,
                _visualProfile.Density,
                _visualProfile.NoiseScale,
                _boundaryConfig.WorldSeed));
            Shader.SetGlobalVector(CloudLightParamsId, new Vector4(
                _visualProfile.LightAbsorption,
                _visualProfile.ScatterEccentricity,
                _visualProfile.PowderStrength,
                _visualProfile.AmbientStrength));
            // unity_SHAr..unity_SHC live in the UnityPerDraw cbuffer, which a full-screen procedural
            // draw never binds, so the environment ambient has to be supplied explicitly.
            RenderSettings.ambientProbe.Evaluate(_ambientDirections, _ambientColors);
            Shader.SetGlobalVector(CloudAmbientSkyId, ToLinearVector(_ambientColors[0]));
            Shader.SetGlobalVector(CloudAmbientGroundId, ToLinearVector(_ambientColors[1]));
            Shader.SetGlobalFloat(CloudFadeStartId, _visualProfile.DistanceFadeStart);
            Shader.SetGlobalVector(CloudWindId, new Vector4(
                _visualProfile.Wind.x,
                _visualProfile.Wind.y,
                0f,
                0f));
            Shader.SetGlobalColor(CloudBaseColorId, _visualProfile.BaseColor);
            Shader.SetGlobalColor(CloudSunColorId, _visualProfile.SunColor);
            Shader.SetGlobalInt(CloudStepCountId, _visualProfile.StepCount);
        }

        private static Vector4 ToLinearVector(Color color)
        {
            return new Vector4(Mathf.Max(color.r, 0f), Mathf.Max(color.g, 0f), Mathf.Max(color.b, 0f), 1f);
        }
    }
}
