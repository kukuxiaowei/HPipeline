using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HPipeline
{
    public class DrawSkyboxPass
    {
        class SkyboxPassData
        {
            internal RenderingData renderingData;
            internal RendererList skyRendererList;
        }

        internal void Render(RenderGraph renderGraph, ScriptableRenderContext context, TextureHandle colorTarget, TextureHandle depthTarget, ref RenderingData renderingData)
        {
            using (var builder = renderGraph.AddRasterRenderPass<SkyboxPassData>("Draw Skybox Pass", out var passData))
            {
                passData.skyRendererList = context.CreateSkyboxRendererList(renderingData.camera);
                builder.UseTextureFragment(colorTarget, 0, IBaseRenderGraphBuilder.AccessFlags.Write);
                builder.UseTextureFragmentDepth(depthTarget, IBaseRenderGraphBuilder.AccessFlags.Write);

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((SkyboxPassData data, RasterGraphContext rgc) =>
                {
                    rgc.cmd.DrawRendererList(data.skyRendererList);
                });
            }
        }
    }
}
