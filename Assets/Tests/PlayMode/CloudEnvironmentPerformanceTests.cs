#nullable enable
using System.Collections;
using NUnit.Framework;
using TinCan.Features.CloudBoundary;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinCan.Tests.PlayMode
{
    public class CloudEnvironmentPerformanceTests
    {
        private const string BoundaryConfigPath = "Assets/Settings/CloudBoundaryConfig.asset";
        private const string VisualProfilePath = "Assets/Settings/CloudVisualProfile.asset";
        private const int RenderWidth = 1280;
        private const int RenderHeight = 720;
        private const int WarmupFrameCount = 30;
        private const int MeasurementFrameCount = 120;

        private GameObject? _fixtureRoot;
        private Camera? _camera;
        private RenderTexture? _renderTexture;
        private int _previousVSyncCount;
        private int _previousTargetFrameRate;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousVSyncCount = QualitySettings.vSyncCount;
            _previousTargetFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            CloudBoundaryConfig boundaryConfig = AssetDatabase.LoadAssetAtPath<CloudBoundaryConfig>(BoundaryConfigPath);
            CloudVisualProfile visualProfile = AssetDatabase.LoadAssetAtPath<CloudVisualProfile>(VisualProfilePath);
            Assert.That(boundaryConfig, Is.Not.Null, $"Missing benchmark asset: {BoundaryConfigPath}");
            Assert.That(visualProfile, Is.Not.Null, $"Missing benchmark asset: {VisualProfilePath}");

            _fixtureRoot = new GameObject("Cloud Performance Fixture");

            var cameraObject = new GameObject("Cloud Performance Camera");
            cameraObject.transform.SetParent(_fixtureRoot.transform, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 120f, -200f);
            cameraObject.transform.LookAt(new Vector3(0f, 45f, 100f));
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 70f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 1500f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;

            var lightObject = new GameObject("Cloud Performance Light");
            lightObject.transform.SetParent(_fixtureRoot.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            var directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1f;

            var environmentObject = new GameObject("Cloud Environment");
            environmentObject.transform.SetParent(_fixtureRoot.transform, false);
            var environment = environmentObject.AddComponent<CloudEnvironmentView>();
            environment.Construct(new CloudSurfaceQuery(boundaryConfig), boundaryConfig, visualProfile);

            yield return null;
            yield return null;

            Assert.That(environment.IsReady, Is.True, "CloudEnvironmentView did not create its surface height map.");
            Assert.That(environment.HeightMap, Is.Not.Null);
            Assert.That(environment.HeightMap!.width, Is.EqualTo(visualProfile.HeightMapResolution));
            Assert.That(environment.HeightMap.height, Is.EqualTo(visualProfile.HeightMapResolution));
            Assert.That(environmentObject.transform.childCount, Is.Zero,
                "Volumetric clouds must not create legacy cloud meshes.");

            Measure.Custom(
                new SampleGroup("Cloud.HeightSampleCount", SampleUnit.Undefined),
                visualProfile.HeightMapResolution * visualProfile.HeightMapResolution);
            Measure.Custom(new SampleGroup("Cloud.RayStepCount", SampleUnit.Undefined), visualProfile.StepCount);
            Measure.Custom(new SampleGroup("Cloud.RenderWidth", SampleUnit.Undefined), RenderWidth);
            Measure.Custom(new SampleGroup("Cloud.RenderHeight", SampleUnit.Undefined), RenderHeight);

            _renderTexture = new RenderTexture(RenderWidth, RenderHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "Cloud Performance Target",
                antiAliasing = 1
            };
            _renderTexture.Create();
            _camera.targetTexture = _renderTexture;
        }

        [UnityTest, Performance]
        public IEnumerator ReferenceView_FrameTime()
        {
            yield return Measure.Frames()
                .SampleGroup("Cloud.FrameTime")
                .WarmupCount(WarmupFrameCount)
                .MeasurementCount(MeasurementFrameCount)
                .Run();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_camera != null)
            {
                _camera.targetTexture = null;
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Object.Destroy(_renderTexture);
            }

            if (_fixtureRoot != null)
            {
                Object.Destroy(_fixtureRoot);
            }

            QualitySettings.vSyncCount = _previousVSyncCount;
            Application.targetFrameRate = _previousTargetFrameRate;
            yield return null;
        }
    }
}
