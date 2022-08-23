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
            public Material BlitMaterial;
        }

        private Material _blitMaterial;

        private void FinalBlitPassInit()
        {
            _blitMaterial = new Material(Shader.Find("Hidden/Blit"));
        }

        private void FinalBlitPassExecute(RenderGraph renderGraph, TextureHandle source, TextureHandle destination)
        {
            using (var builder = renderGraph.AddRenderPass<FinalBlitPassData>("FinalBlit Pass", out var passData))
            {
                passData.Source = builder.ReadTexture(source);
                passData.Destination = builder.WriteTexture(destination);
                passData.BlitMaterial = _blitMaterial;

                builder.SetRenderFunc((FinalBlitPassData data, RenderGraphContext context) =>
                {
                    context.cmd.Blit(data.Source, data.Destination, data.BlitMaterial);
                });
            }
        }

        private void FinalBlitPassDispose()
        {
            Destroy(_blitMaterial);
        }
    }
}