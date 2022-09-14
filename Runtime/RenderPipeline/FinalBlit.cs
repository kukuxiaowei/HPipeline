using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

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

        private void FinalBlitPassExecute(RenderGraph renderGraph, Camera camera, TextureHandle source, TextureHandle destination)
        {
            using (var builder = renderGraph.AddRenderPass<FinalBlitPassData>("FinalBlit Pass", out var passData))
            {
                passData.Source = builder.ReadTexture(source);
                passData.Destination = builder.UseColorBuffer(destination, 0);
                passData.BlitMaterial = _blitMaterial;

                builder.SetRenderFunc((FinalBlitPassData data, RenderGraphContext context) =>
                {
                    RTHandle sourceTexture = data.Source;
                    var pixelRect = camera.pixelRect;
                    var blitScaleOffset = new Vector4(pixelRect.width / sourceTexture.rt.width, pixelRect.height / sourceTexture.rt.height, 0, 0);
                    if (camera.cameraType == CameraType.Game)
                    {
                        blitScaleOffset.w = blitScaleOffset.y;
                        blitScaleOffset.y *= -1.0f;
                    }
                    
                    var propertyBlock = context.renderGraphPool.GetTempMaterialPropertyBlock();
                    propertyBlock.SetVector(ShaderIDs._BlitScaleOffset, blitScaleOffset);
                    propertyBlock.SetTexture(ShaderIDs._Source, data.Source);
                    
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.BlitMaterial, 0, MeshTopology.Triangles, 3, 1, propertyBlock);
                });
            }
        }

        private void FinalBlitPassDispose()
        {
            Destroy(_blitMaterial);
        }
    }
}