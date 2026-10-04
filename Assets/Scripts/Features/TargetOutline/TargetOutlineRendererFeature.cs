#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TinCan.Features.TargetOutline
{
    /// <summary>
    /// URP renderer feature (on _MainURPRenderer): outlines renderers on the outline rendering layer
    /// (<see cref="TargetOutlineConfig.RenderingLayer"/>). Two render-graph passes after transparents: the marked
    /// renderers are drawn white into a one-channel mask, then a full-screen pass draws the outline colour where a pixel
    /// is outside the mask but within <see cref="TargetOutlineConfig.Width"/> pixels of it. Constant width on any mesh,
    /// no material changes. Does nothing while nothing is outlined, and only for game cameras.
    /// </summary>
    public sealed class TargetOutlineRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private TargetOutlineConfig? _config;

        private Material? _material;
        private TargetOutlinePass? _pass;

        public override void Create()
        {
            if (_config == null || _config.Shader == null) return;
            _material = CoreUtils.CreateEngineMaterial(_config.Shader);
            _pass = new TargetOutlinePass(_config, _material) { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _config == null || _config.Highlighted == 0) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);

        private sealed class TargetOutlinePass : ScriptableRenderPass
        {
            private const int MaskPass = 0;
            private const int EdgePass = 1;
            private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
            private static readonly List<ShaderTagId> ShaderTags = new()
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            private readonly TargetOutlineConfig _config;
            private readonly Material _material;

            public TargetOutlinePass(TargetOutlineConfig config, Material material)
            {
                _config = config;
                _material = material;
            }

            private sealed class MaskData
            {
                public RendererListHandle Renderers;
            }

            private sealed class EdgeData
            {
                public TextureHandle Mask;
                public Material Material = null!;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var lights = frameData.Get<UniversalLightData>();

                var desc = renderGraph.GetTextureDesc(resources.activeColorTexture);
                desc.name = "_TargetOutlineMask";
                desc.format = GraphicsFormat.R8_UNorm;
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                TextureHandle mask = renderGraph.CreateTexture(desc);

                var drawing = RenderingUtils.CreateDrawingSettings(ShaderTags, rendering, camera, lights, SortingCriteria.None);
                drawing.overrideMaterial = _material;
                drawing.overrideMaterialPassIndex = MaskPass;
                var filtering = new FilteringSettings(RenderQueueRange.all) { renderingLayerMask = _config.LayerBit };
                var renderers = renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));

                using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Target Outline Mask", out var data))
                {
                    data.Renderers = renderers;
                    builder.UseRendererList(renderers);
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderFunc((MaskData pass, RasterGraphContext context) => context.cmd.DrawRendererList(pass.Renderers));
                }

                _material.SetColor(ColorId, _config.Color);
                _material.SetFloat(WidthId, _config.Width);
                using (var builder = renderGraph.AddRasterRenderPass<EdgeData>("Target Outline Edge", out var data))
                {
                    data.Mask = mask;
                    data.Material = _material;
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc((EdgeData pass, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, pass.Mask, new Vector4(1f, 1f, 0f, 0f), pass.Material, EdgePass));
                }
            }
        }
    }
}
