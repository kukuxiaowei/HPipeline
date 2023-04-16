using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HPipeline
{
    public class FinalBlitPass
    {
        Material m_BlitMaterial;
        MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();

        public FinalBlitPass(Material blitMaterial)
        {
            m_BlitMaterial = blitMaterial;
        }

        void ExecutePass(RasterCommandBuffer cmd, FinalBlitPassData data, RTHandle source, ref RenderingData renderingData)
        {
            var camera = renderingData.camera;
            bool isRenderToBackBufferTarget = camera.cameraType != CameraType.SceneView;
            Vector2 viewportScale = source.useScaling ? new Vector2(source.rtHandleProperties.rtHandleScale.x, source.rtHandleProperties.rtHandleScale.y) : Vector2.one;

            // We y-flip if
            // 1) we are blitting from render texture to back buffer(UV starts at bottom) and
            // 2) renderTexture starts UV at top
            bool yflip = isRenderToBackBufferTarget && camera.targetTexture == null && SystemInfo.graphicsUVStartsAtTop;
            Vector4 scaleBias = yflip ? new Vector4(viewportScale.x, -viewportScale.y, 0, viewportScale.y) : new Vector4(viewportScale.x, viewportScale.y, 0, 0);
            if (isRenderToBackBufferTarget)
                cmd.SetViewport(camera.pixelRect);

            var shaderPass = 0;// source.rt?.filterMode == FilterMode.Bilinear ? k_FinalBlitBilinearSamplerShaderPass : k_FinalBlitPointSamplerShaderPass;

            m_PropertyBlock.SetVector(ShaderPropertyId.blitScaleBias, scaleBias);
            m_PropertyBlock.SetTexture(ShaderPropertyId.blitTexture, source);
            cmd.DrawProcedural(Matrix4x4.identity, data.blitMaterial, shaderPass, MeshTopology.Triangles, 3, 1, m_PropertyBlock);
        }

        class FinalBlitPassData
        {
            internal TextureHandle source;
            internal TextureHandle destination;
            internal Material blitMaterial;
            internal RenderingData renderingData;
        }

        internal void Render(RenderGraph renderGraph, ref RenderingData renderingData, TextureHandle src, TextureHandle dest)
        {
            using (var builder = renderGraph.AddRasterRenderPass<FinalBlitPassData>("Final Blit Pass", out var passData))
            {
                passData.renderingData = renderingData;
                passData.blitMaterial = m_BlitMaterial;

                passData.source = builder.UseTexture(src, IBaseRenderGraphBuilder.AccessFlags.Read);
                passData.destination = builder.UseTextureFragment(dest, 0, IBaseRenderGraphBuilder.AccessFlags.Write);

                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((FinalBlitPassData data, RasterGraphContext context) =>
                {
                    //CoreUtils.SetKeyword(context.cmd, ShaderKeywordStrings.LinearToSRGBConversion, data.requireSrgbConversion);

                    ExecutePass(context.cmd, data, data.source, ref data.renderingData);
                });
            }
        }
    }
}