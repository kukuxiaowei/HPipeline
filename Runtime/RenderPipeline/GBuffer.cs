using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HPipeline
{
    internal class GBufferPass
    {
        internal static readonly string[] k_GBufferNames = new string[]
        {
            "_GBuffer0",
            "_GBuffer1",
            "_GBuffer2",
            "_BakedGI",
        };

        static ShaderTagId s_ShaderTagGBuffer = new ShaderTagId("GBuffer");

        FilteringSettings m_FilteringSettings;

        internal TextureHandle[] GbufferTextureHandles { get; set; }

        public GBufferPass()
        {
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        }

        class GBufferPassData
        {
            internal TextureHandle[] gbuffer;
            internal TextureHandle depth;

            internal RendererListHandle rendererListHdl;
        }

        private void InitRendererLists(ref RenderingData renderingData, ref GBufferPassData passData, RenderGraph renderGraph)
        {
            ShaderTagId lightModeTag = s_ShaderTagGBuffer;
            var drawingSettings = CreateDrawingSettings(lightModeTag, ref renderingData, SortingCriteria.CommonOpaque);
            var filterSettings = m_FilteringSettings;
            var param = new RendererListParams(renderingData.cullResults, drawingSettings, filterSettings);
            passData.rendererListHdl = renderGraph.CreateRendererList(param);
        }

        internal void CreateGBufferTexture(RenderGraph renderGraph, ref RenderingData renderingData, FrameResources frameResources)
        {
            GbufferTextureHandles = new TextureHandle[4];

            var normalDescriptor = renderingData.cameraTargetDescriptor;
            normalDescriptor.graphicsFormat = GraphicsFormat.A2B10G10R10_UNormPack32;
            GbufferTextureHandles[0] = RenderingUtils.CreateRenderGraphTexture(renderGraph, normalDescriptor, "_GBuffer0", true);

            var albedoDescriptor = renderingData.cameraTargetDescriptor;
            albedoDescriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;
            GbufferTextureHandles[1] = RenderingUtils.CreateRenderGraphTexture(renderGraph, albedoDescriptor, "_GBuffer1", true);

            var specularDescriptor = renderingData.cameraTargetDescriptor;
            specularDescriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            GbufferTextureHandles[2] = RenderingUtils.CreateRenderGraphTexture(renderGraph, albedoDescriptor, "_GBuffer2", true);

            GbufferTextureHandles[3] = RenderingUtils.CreateRenderGraphTexture(renderGraph, renderingData.cameraTargetDescriptor, "_GBuffer3", true);

            frameResources.SetFrameResourcesGBufferArray(GbufferTextureHandles);
        }

        internal void Render(RenderGraph renderGraph, TextureHandle depthBuffer, ref RenderingData renderingData, FrameResources frameResources)
        {
            using (var builder = renderGraph.AddRasterRenderPass<GBufferPassData>("GBuffer Pass", out var passData))
            {
                passData.gbuffer = GbufferTextureHandles = frameResources.GetFrameResourcesGBufferArray(4);

                for(int i = 0; i < 4; i++)
                {
                    passData.gbuffer[i] = builder.UseTextureFragment(GbufferTextureHandles[i], i, IBaseRenderGraphBuilder.AccessFlags.Write);
                }

                passData.depth = builder.UseTextureFragmentDepth(depthBuffer, IBaseRenderGraphBuilder.AccessFlags.Write);

                InitRendererLists(ref renderingData, ref passData, renderGraph);
                builder.UseRendererList(passData.rendererListHdl);

                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((GBufferPassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.rendererListHdl);
                });
            }
        }


        /// <summary>
        /// Creates <c>DrawingSettings</c> based on current the rendering state.
        /// </summary>
        /// <param name="shaderTagId">Shader pass tag to render.</param>
        /// <param name="renderingData">Current rendering state.</param>
        /// <param name="sortingCriteria">Criteria to sort objects being rendered.</param>
        /// <returns></returns>
        /// <seealso cref="DrawingSettings"/>
        static DrawingSettings CreateDrawingSettings(ShaderTagId shaderTagId, ref RenderingData renderingData, SortingCriteria sortingCriteria)
        {
            Camera camera = renderingData.camera;
            SortingSettings sortingSettings = new SortingSettings(camera) { criteria = sortingCriteria };
            DrawingSettings settings = new DrawingSettings(shaderTagId, sortingSettings)
            {
                perObjectData = PerObjectData.Lightmaps,
                //enableDynamicBatching = renderingData.supportsDynamicBatching,

                // Disable instancing for preview cameras. This is consistent with the built-in forward renderer. Also fixes case 1127324.
                enableInstancing = camera.cameraType == CameraType.Preview ? false : true,
            };
            return settings;
        }
    }
}