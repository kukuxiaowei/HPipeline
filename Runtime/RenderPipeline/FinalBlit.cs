using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HPipeline
{
    public partial class RenderPipeline
    {
        private class FinalBlitPassData
        {
            public TextureHandle Source;
            public TextureHandle Destination;
        }

        private void FinalBlitPassExecute(RenderGraph renderGraph, TextureHandle source, TextureHandle destination)
        {
            using (var builder = renderGraph.AddRenderPass<FinalBlitPassData>("FinalBlit Pass", out var passData))
            {
                passData.Source = builder.ReadTexture(source);
                passData.Destination = builder.WriteTexture(destination);

                builder.SetRenderFunc((FinalBlitPassData data, RenderGraphContext context) =>
                {
                    context.cmd.Blit(data.Source, data.Destination, GetBlitMaterial());
                });
            }
        }
    }
}