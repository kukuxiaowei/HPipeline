using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace HPipeline
{
    public partial class RenderPipeline
    {
        private const int clusterResX = 32;
        private const int clusterResY = 32;
        private const int clustersNumZ = 16;
        private const int clusterMaxLightCount = 32;

        private ComputeShader _clusterLightsCullCS;
        private int _kernel;
        
        void ClusterLightsCullPassInit()
        {
            _clusterLightsCullCS = Resources.Load<ComputeShader>("ClusterLightsCullCS");
            _kernel = _clusterLightsCullCS.FindKernel("ClusterLightsCull");
        }
        
        class ClusterLightsCullPassData
        {
            public ComputeShader ClusterLightsCullCS;
            public int Kernel;
            public TextureHandle LightsCullTexture;
            public ComputeBufferHandle LightIndexBuffer;
            public ComputeBuffer LightData;
        }
        
        void ClusterLightsCullPassExecute(RenderGraph renderGraph, Camera camera, ComputeBuffer lightData)
        {
            using (var builder = renderGraph.AddRenderPass<ClusterLightsCullPassData>("ClusterLightsCull Pass", out var passData))
            {
                builder.EnableAsyncCompute(true);

                var pixelRect = camera.pixelRect;
                int clustersNumX = Mathf.CeilToInt(pixelRect.width / clusterResX);
                int clustersNumY = Mathf.CeilToInt(pixelRect.height / clusterResY);

                passData.ClusterLightsCullCS = _clusterLightsCullCS;
                passData.Kernel = _kernel;
                var lightsCullTexture = renderGraph.CreateTexture(new TextureDesc(clustersNumX, clustersNumY)
                { 
                    slices = clustersNumZ,
                    colorFormat = GraphicsFormat.R16G16_UInt,
                    dimension = TextureDimension.Tex3D,
                    enableRandomWrite = true,
                    name = "LightsCullTexture"
                });
                passData.LightsCullTexture = builder.WriteTexture(lightsCullTexture);
                var lightIndexBuffer = renderGraph.CreateComputeBuffer(new ComputeBufferDesc(clustersNumX * clustersNumY * clustersNumZ * clusterMaxLightCount, sizeof(uint))
                { 
                    name = "LightIndexBuffer"
                });
                passData.LightIndexBuffer = builder.WriteComputeBuffer(lightIndexBuffer);
                passData.LightData = lightData;

                builder.SetRenderFunc((ClusterLightsCullPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(ShaderIDs._ClustersNumData, 
                        new Vector4(clustersNumX, clustersNumY, clusterResX / camera.pixelRect.width, clusterResY / camera.pixelRect.height));
                    float tanFov = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad);
                    context.cmd.SetGlobalVector(ShaderIDs._ClusterSizeData, 
                        new Vector4(tanFov * 2 * camera.aspect, tanFov * 2, camera.farClipPlane / clustersNumZ, clustersNumZ / camera.farClipPlane));
                    context.cmd.SetComputeTextureParam(data.ClusterLightsCullCS, data.Kernel, ShaderIDs._LightsCullTexture, data.LightsCullTexture);
                    context.cmd.SetComputeBufferParam(data.ClusterLightsCullCS, data.Kernel, ShaderIDs._LightIndexBuffer, data.LightIndexBuffer);
                    context.cmd.SetComputeBufferParam(data.ClusterLightsCullCS, data.Kernel, ShaderIDs._LightData, data.LightData);
                    context.cmd.DispatchCompute(data.ClusterLightsCullCS, data.Kernel, clustersNumX, clustersNumY, 1);
                });
            }
        }
    }
}