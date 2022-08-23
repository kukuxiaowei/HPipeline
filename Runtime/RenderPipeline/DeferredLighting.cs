using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HPipeline
{
    public partial class RenderPipeline
    {
        private class DeferredLightingPassData
        {
            public GBuffer GBufferData;
            public TextureHandle ColorBuffer;
            public Material DeferredLightingMaterial;
        }

        private Material _deferredLightingMaterial;

        private void DeferredLightingPassInit()
        {
            _deferredLightingMaterial = new Material(Shader.Find("Hidden/DeferredLighting"));
        }

        private void DeferredLightingPassExecute(RenderGraph renderGraph, GBuffer gBuffer, out TextureHandle colorBuffer)
        {
            using (var builder = renderGraph.AddRenderPass<DeferredLightingPassData>("DeferredLighting Pass", out var passData))
            {
                passData.GBufferData.DepthBuffer = builder.ReadTexture(gBuffer.DepthBuffer);
                passData.GBufferData.GBuffer0 = builder.ReadTexture(gBuffer.GBuffer0);
                passData.GBufferData.GBuffer1 = builder.ReadTexture(gBuffer.GBuffer1);
                passData.GBufferData.GBuffer2 = builder.ReadTexture(gBuffer.GBuffer2);
                colorBuffer = renderGraph.CreateTexture(new TextureDesc(Vector2.one)
                {
                    colorFormat = GraphicsFormat.B10G11R11_UFloatPack32, 
                    clearBuffer = true, 
                    clearColor = Color.black,
                    name = "DeferredLighting"
                });
                passData.ColorBuffer = builder.WriteTexture(colorBuffer);
                passData.DeferredLightingMaterial = _deferredLightingMaterial;

                builder.SetRenderFunc((DeferredLightingPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(ShaderIDs._DepthBuffer, data.GBufferData.DepthBuffer);
                    context.cmd.SetGlobalTexture(ShaderIDs._GBuffer0, data.GBufferData.GBuffer0);
                    context.cmd.SetGlobalTexture(ShaderIDs._GBuffer1, data.GBufferData.GBuffer1);
                    context.cmd.SetGlobalTexture(ShaderIDs._GBuffer2, data.GBufferData.GBuffer2);
                    context.cmd.Blit(null, data.ColorBuffer, data.DeferredLightingMaterial);
                });
            }
        }

        private void DeferredLightingPassDispose()
        {
            Destroy(_deferredLightingMaterial);
        }
    }
}