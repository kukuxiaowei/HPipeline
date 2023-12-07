using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HPipeline
{
    internal class DeferredLightingPass
    {
        Material m_DeferredLightingMaterial;

        public DeferredLightingPass(Material deferredLightingMaterial)
        {
            m_DeferredLightingMaterial = deferredLightingMaterial;
        }

        class DeferredLightingPassData
        {
            internal TextureHandle color;
            internal TextureHandle depth;
            internal TextureHandle[] gbuffer;
            internal BuildClusterLightGridResult buildClusterLightGridResult;
        }

        internal void Render(RenderGraph renderGraph, out TextureHandle color, TextureHandle depth, in BuildClusterLightGridResult buildClusterLightGridResult, ref RenderingData renderingData, FrameResources frameResources)
        {
            var gbuffer = frameResources.GetFrameResourcesGBufferArray(4);

            var colorDescriptor = renderingData.cameraTargetDescriptor;
            colorDescriptor.useMipMap = false;
            colorDescriptor.autoGenerateMips = false;
            colorDescriptor.depthBufferBits = (int)DepthBits.None;

            color = RenderingUtils.CreateRenderGraphTexture(renderGraph, colorDescriptor, "_DeferredLightingTexture", true);

            using (var builder = renderGraph.AddRenderPass<DeferredLightingPassData>("Deferred Lighting Pass", out var passData))
            {
                passData.color = builder.WriteTexture(color);
                passData.depth = builder.ReadTexture(depth);

                passData.gbuffer = gbuffer;
                for (int i = 0; i < gbuffer.Length; ++i)
                {
                    passData.gbuffer[i] = builder.ReadTexture(gbuffer[i]);
                }

                passData.buildClusterLightGridResult.clusterPackingOffset = builder.ReadBuffer(buildClusterLightGridResult.clusterPackingOffset);
                passData.buildClusterLightGridResult.clusterLights = builder.ReadBuffer(buildClusterLightGridResult.clusterLights);

                builder.SetRenderFunc((DeferredLightingPassData data, RenderGraphContext context) =>
                {
                    m_DeferredLightingMaterial.SetTexture(ShaderPropertyId.depthBuffer, data.depth);
                    m_DeferredLightingMaterial.SetTexture(ShaderPropertyId.gBuffer0, data.gbuffer[0]);
                    m_DeferredLightingMaterial.SetTexture(ShaderPropertyId.gBuffer1, data.gbuffer[1]);
                    m_DeferredLightingMaterial.SetTexture(ShaderPropertyId.gBuffer2, data.gbuffer[2]);
                    m_DeferredLightingMaterial.SetTexture(ShaderPropertyId.bakedGI, data.gbuffer[3]);
                    m_DeferredLightingMaterial.SetBuffer(ShaderPropertyId.clusterPackingOffset, data.buildClusterLightGridResult.clusterPackingOffset);
                    m_DeferredLightingMaterial.SetBuffer(ShaderPropertyId.clusterLights, data.buildClusterLightGridResult.clusterLights);

                    context.cmd.DrawProcedural(Matrix4x4.identity, m_DeferredLightingMaterial, 0, MeshTopology.Triangles, 3, 1);
                });
            }
        }
    }
}