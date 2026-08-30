#nullable enable
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TinCan.Features.CloudBoundary.Rendering
{
    public class VolumetricCloudRendererFeature : ScriptableRendererFeature
    {
        private static readonly int CloudEnabledId = Shader.PropertyToID("_CloudEnabled");

        [SerializeField] private Shader? _shader;
        [SerializeField] private RenderPassEvent _renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        [SerializeField, Range(1, 4)] private int _resolutionDivisor = 2;

        private Material? _material;
        private VolumetricCloudRenderPass? _renderPass;

        public override void Create()
        {
            CoreUtils.Destroy(_material);
            _material = _shader != null ? CoreUtils.CreateEngineMaterial(_shader) : null;
            _renderPass = new VolumetricCloudRenderPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || _renderPass == null)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
            {
                return;
            }

            // CloudEnvironmentView publishes the volume parameters as shader globals. Without them the
            // ray march has no valid layer to integrate, so the camera colour must be left untouched.
            if (Shader.GetGlobalFloat(CloudEnabledId) < 0.5f)
            {
                return;
            }

            _renderPass.Setup(_material, _renderPassEvent, _resolutionDivisor);
            renderer.EnqueuePass(_renderPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _renderPass = null;
        }

        private sealed class VolumetricCloudRenderPass : ScriptableRenderPass
        {
            private static readonly int CloudSourceColorId = Shader.PropertyToID("_CloudSourceColor");
            private static readonly int CloudTextureId = Shader.PropertyToID("_CloudTexture");
            private readonly MaterialPropertyBlock _compositeProperties = new();
            private Material? _material;
            private int _resolutionDivisor = 2;

            public void Setup(Material material, RenderPassEvent passEvent, int resolutionDivisor)
            {
                _material = material;
                _resolutionDivisor = Mathf.Max(1, resolutionDivisor);
                renderPassEvent = passEvent;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                TextureHandle cameraColor = resourceData.activeColorTexture;
                TextureHandle sceneDepth = resourceData.cameraDepthTexture;
                if (!cameraColor.IsValid() || !sceneDepth.IsValid())
                {
                    return;
                }

                TextureDesc cloudDescriptor = renderGraph.GetTextureDesc(cameraColor);
                cloudDescriptor.name = "Volumetric Clouds";
                cloudDescriptor.width = Mathf.Max(1, cloudDescriptor.width / _resolutionDivisor);
                cloudDescriptor.height = Mathf.Max(1, cloudDescriptor.height / _resolutionDivisor);
                cloudDescriptor.msaaSamples = MSAASamples.None;
                cloudDescriptor.bindTextureMS = false;
                cloudDescriptor.useMipMap = false;
                cloudDescriptor.autoGenerateMips = false;
                cloudDescriptor.filterMode = FilterMode.Bilinear;
                cloudDescriptor.wrapMode = TextureWrapMode.Clamp;
                cloudDescriptor.clearBuffer = true;
                cloudDescriptor.clearColor = Color.clear;
                TextureHandle cloudTexture = renderGraph.CreateTexture(cloudDescriptor);

                TextureDesc compositeDescriptor = renderGraph.GetTextureDesc(cameraColor);
                compositeDescriptor.name = "Volumetric Clouds Composite";
                compositeDescriptor.msaaSamples = MSAASamples.None;
                compositeDescriptor.bindTextureMS = false;
                compositeDescriptor.clearBuffer = false;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDescriptor);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CloudPassData>(
                           "Volumetric Clouds Ray March",
                           out CloudPassData passData))
                {
                    passData.Material = _material;
                    builder.UseTexture(sceneDepth, AccessFlags.Read);
                    // The ray march samples _CameraDepthTexture and the main light constants, which URP
                    // publishes as render graph globals rather than as pass-local bindings.
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(cloudTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (CloudPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3, 1);
                    });
                }

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                           "Volumetric Clouds Composite",
                           out CompositePassData passData))
                {
                    passData.Material = _material;
                    passData.SourceColor = cameraColor;
                    passData.CloudTexture = cloudTexture;
                    passData.Properties = _compositeProperties;
                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    builder.UseTexture(cloudTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(compositeTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                    {
                        data.Properties.SetTexture(CloudSourceColorId, data.SourceColor);
                        data.Properties.SetTexture(CloudTextureId, data.CloudTexture);
                        context.cmd.DrawProcedural(
                            Matrix4x4.identity,
                            data.Material,
                            1,
                            MeshTopology.Triangles,
                            3,
                            1,
                            data.Properties);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private sealed class CloudPassData
            {
                public Material Material = null!;
            }

            private sealed class CompositePassData
            {
                public Material Material = null!;
                public TextureHandle SourceColor;
                public TextureHandle CloudTexture;
                public MaterialPropertyBlock Properties = null!;
            }
        }
    }
}
