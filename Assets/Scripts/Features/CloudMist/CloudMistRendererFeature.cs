#nullable enable
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TinCan.Features.CloudMist
{
    /// <summary>
    /// URP renderer feature (on _MainURPRenderer): the cloud sea as mist near the camera (plan
    /// .docs/plans/cloud-sea-fog.md, F4; shader Shaders/CloudSeaMist.shader). Two render-graph passes after transparents:
    /// a raymarch through the slab the mist can fill, at the active tier's resolution, then a depth-aware upsample onto the
    /// camera's colour. The mist's look is its material (<see cref="_material"/>, copied each frame so tuning it in Play
    /// shows); the tier (<see cref="CloudMistQuality.Active"/>) only sets how well it is sampled. Draws nothing while no
    /// tier is active, and only for game cameras that are not overlays.
    /// </summary>
    public sealed class CloudMistRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private CloudMistQuality? _quality;
        [Tooltip("The mist's look: Assets/Materials/CloudSea/M_CloudSeaMist (TinCan/Environment/Cloud Sea Mist).")]
        [SerializeField] private Material? _material;

        private Material? _runtime;
        private CloudMistPass? _pass;

        public override void Create()
        {
            if (_material == null) return;
            _runtime = new Material(_material) { name = $"{_material.name} (runtime)", hideFlags = HideFlags.DontSave };
            _pass = new CloudMistPass(_runtime) { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _runtime == null || _material == null || _quality == null) return;
            if (_quality.Active is not { } tier) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            if (renderingData.cameraData.renderType == CameraRenderType.Overlay) return;

            _runtime.CopyPropertiesFromMaterial(_material);
            _pass.Tier = tier;
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(_runtime);

        private sealed class CloudMistPass : ScriptableRenderPass
        {
            private const int MarchPass = 0;
            private const int CompositePass = 1;
            private static readonly int StepsId = Shader.PropertyToID("_MistSteps");
            private static readonly int LightStepsId = Shader.PropertyToID("_MistLightSteps");
            private static readonly int OctavesId = Shader.PropertyToID("_MistOctaves");
            private static readonly int ShadowsId = Shader.PropertyToID("_MistShadows");
            private static readonly int RangeId = Shader.PropertyToID("_MistRange");
            private static readonly int FrameId = Shader.PropertyToID("_MistFrame");
            private static readonly int SizeId = Shader.PropertyToID("_MistSize");

            private readonly Material _material;

            public CloudMistPass(Material material) => _material = material;

            public CloudMistTier Tier { get; set; }

            private sealed class MarchData
            {
                public Material Material = null!;
            }

            private sealed class CompositeData
            {
                public TextureHandle Mist;
                public Material Material = null!;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid()) return;

                var desc = renderGraph.GetTextureDesc(resources.activeColorTexture);
                desc.name = "_CloudMist";
                desc.width = Mathf.Max(1, Mathf.RoundToInt(desc.width * Tier.Resolution));
                desc.height = Mathf.Max(1, Mathf.RoundToInt(desc.height * Tier.Resolution));
                desc.format = GraphicsFormat.R16G16B16A16_SFloat;
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                desc.clearBuffer = false;
                desc.filterMode = FilterMode.Point;
                TextureHandle mist = renderGraph.CreateTexture(desc);

                _material.SetFloat(StepsId, Tier.Steps);
                _material.SetFloat(LightStepsId, Tier.LightSteps);
                _material.SetFloat(OctavesId, Tier.DetailOctaves);
                _material.SetFloat(ShadowsId, Tier.Shadows ? 1f : 0f);
                _material.SetFloat(RangeId, Tier.Range);
                _material.SetFloat(FrameId, Time.frameCount % 64);
                _material.SetVector(SizeId, new Vector4(desc.width, desc.height, 1f / desc.width, 1f / desc.height));

                using (var builder = renderGraph.AddRasterRenderPass<MarchData>("Cloud Mist March", out var data))
                {
                    data.Material = _material;
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    // The main light's shadow map and cookie, read as globals.
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(mist, 0, AccessFlags.Write);
                    builder.SetRenderFunc((MarchData pass, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), pass.Material, MarchPass));
                }

                using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("Cloud Mist Composite", out var data))
                {
                    data.Mist = mist;
                    data.Material = _material;
                    builder.UseTexture(mist, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc((CompositeData pass, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, pass.Mist, new Vector4(1f, 1f, 0f, 0f), pass.Material, CompositePass));
                }
            }
        }
    }
}
