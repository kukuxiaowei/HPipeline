using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;

namespace HPipeline
{
    public partial class RenderPipeline
    {
        private struct GBuffer
        {
            public TextureHandle DepthBuffer;
            public TextureHandle GBuffer0;
            public TextureHandle GBuffer1;
            public TextureHandle GBuffer2;
            public TextureHandle BakedGI;
        }

        private class GBufferPassData
        {
            public RendererListHandle RendererList;
        }
        
        void GBufferPassExecute(RenderGraph renderGraph, CullingResults cullingResults, Camera camera, out GBuffer gBuffer)
        {
            using (var builder = renderGraph.AddRenderPass<GBufferPassData>("GBuffer Pass", out var passData))
            {
                var depthBuffer = renderGraph.CreateTexture(new TextureDesc(Vector2.one)
                {
                    depthBufferBits = DepthBits.Depth32,
                    filterMode = FilterMode.Point,
                    clearBuffer = true,
                    name = "DepthBuffer"
                });
                var gBuffer0 = renderGraph.CreateTexture(new TextureDesc(Vector2.one)
                {
                    colorFormat = GraphicsFormat.A2B10G10R10_UNormPack32,
                    filterMode = FilterMode.Point,
                    clearBuffer = true,
                    clearColor = Color.black,
                    name = "GBuffer0"
                });
                var gBuffer1 = renderGraph.CreateTexture(new TextureDesc(Vector2.one)
                {
                    colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                    filterMode = FilterMode.Point,
                    clearBuffer = true,
                    clearColor = Color.black,
                    name = "GBuffer1"
                });
                var gBuffer2 = renderGraph.CreateTexture(new TextureDesc(Vector2.one)
                {
                    colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                    filterMode = FilterMode.Point,
                    clearBuffer = true,
                    clearColor = Color.black,
                    name = "GBuffer2"
                });
                var bakedGI = renderGraph.CreateTexture(new TextureDesc(Vector2.one)
                {
                    colorFormat = GraphicsFormat.B10G11R11_UFloatPack32,
                    filterMode = FilterMode.Point,
                    clearBuffer = true,
                    clearColor = Color.black,
                    name = "BakedGI"
                });
                gBuffer = new GBuffer
                {
                    DepthBuffer = builder.UseDepthBuffer(depthBuffer, DepthAccess.ReadWrite),
                    GBuffer0 = builder.UseColorBuffer(gBuffer0, 0),
                    GBuffer1 = builder.UseColorBuffer(gBuffer1, 1),
                    GBuffer2 = builder.UseColorBuffer(gBuffer2, 2),
                    BakedGI = builder.UseColorBuffer(bakedGI, 3),
                };

                passData.RendererList = builder.UseRendererList(renderGraph.CreateRendererList(
                    new RendererListDesc(ShaderIDs.GBuffer, cullingResults, camera)
                    {
                        rendererConfiguration = PerObjectData.Lightmaps,
                        renderQueueRange = RenderQueueRange.opaque, 
                        sortingCriteria = SortingCriteria.CommonOpaque
                    }));
                
                builder.SetRenderFunc(
                    (GBufferPassData data, RenderGraphContext context) =>
                    {
                        //Unity2021
                        context.cmd.DrawRendererList(data.RendererList);
                    });
            }
        }
    }
}