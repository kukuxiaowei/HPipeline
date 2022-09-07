using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace HPipeline
{
    public partial class RenderPipeline
    {
        private struct ClusterLightsCullResult
        {
            public TextureHandle LightsCullTexture;
            public ComputeBufferHandle LightIndexBuffer;
        }

        class ClusterLightsCullPassData
        {
            public ComputeShader ClusterLightsCullCS;
            public ClusterLightsCullResult ClusterLightsCullResult;
            public ComputeBufferHandle LightStartOffsetCounter;
            public ComputeBuffer LightData;
        }

        private const int clusterResX = 32;
        private const int clusterResY = 32;
        private const int clustersNumZ = 16;

        private ComputeShader _clusterLightsCullCS;
        private int _clusterLightsCullKernel;
        private int _clearCounterKernel;
        
        void ClusterLightsCullPassInit()
        {
            _clusterLightsCullCS = Resources.Load<ComputeShader>("ClusterLightsCullCS");
            _clusterLightsCullKernel = _clusterLightsCullCS.FindKernel("ClusterLightsCull");
            _clearCounterKernel = _clusterLightsCullCS.FindKernel("ClearCounter");
        }
        
        void ClusterLightsCullPassExecute(RenderGraph renderGraph, Camera camera, ComputeBuffer lightData, out ClusterLightsCullResult clusterLightsCullResult)
        {
            using (var builder = renderGraph.AddRenderPass<ClusterLightsCullPassData>("ClusterLightsCull Pass", out var passData))
            {
                builder.EnableAsyncCompute(true);

                var pixelRect = camera.pixelRect;
                int clustersNumX = Mathf.CeilToInt(pixelRect.width / clusterResX);
                int clustersNumY = Mathf.CeilToInt(pixelRect.height / clusterResY);

                passData.ClusterLightsCullCS = _clusterLightsCullCS;
                var lightsCullTexture = renderGraph.CreateTexture(new TextureDesc(clustersNumX, clustersNumY)
                { 
                    slices = clustersNumZ,
                    colorFormat = GraphicsFormat.R16G16_UInt,
                    filterMode = FilterMode.Point,
                    dimension = TextureDimension.Tex3D,
                    enableRandomWrite = true,
                    name = "LightsCullTexture"
                });
                passData.ClusterLightsCullResult.LightsCullTexture = builder.WriteTexture(lightsCullTexture);
                var lightIndexBuffer = renderGraph.CreateComputeBuffer(new ComputeBufferDesc(65536, sizeof(uint))
                { 
                    name = "LightIndexBuffer"
                });
                var lightStartOffsetCounter = renderGraph.CreateComputeBuffer(new ComputeBufferDesc(1, sizeof(uint))
                { 
                    name = "LightStartOffsetCounter"
                });
                passData.ClusterLightsCullResult.LightIndexBuffer = builder.WriteComputeBuffer(lightIndexBuffer);
                passData.LightStartOffsetCounter = builder.WriteComputeBuffer(lightStartOffsetCounter);
                passData.LightData = lightData;

                clusterLightsCullResult = new ClusterLightsCullResult
                {
                    LightsCullTexture = lightsCullTexture,
                    LightIndexBuffer = lightIndexBuffer,
                };

                builder.SetRenderFunc((ClusterLightsCullPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeBufferParam(data.ClusterLightsCullCS, _clearCounterKernel, ShaderIDs._LightStartOffsetCounter, data.LightStartOffsetCounter);
                    context.cmd.DispatchCompute(data.ClusterLightsCullCS, _clearCounterKernel, 1, 1, 1);
                    
                    context.cmd.SetGlobalVector(ShaderIDs._ClustersNumData, 
                        new Vector4(clustersNumX, clustersNumY, clusterResX / camera.pixelRect.width, clusterResY / camera.pixelRect.height));
                    float tanFov = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    context.cmd.SetGlobalVector(ShaderIDs._ClusterSizeData, 
                        new Vector4(tanFov * camera.aspect, tanFov, camera.farClipPlane / clustersNumZ, clustersNumZ / camera.farClipPlane));
                    context.cmd.SetComputeTextureParam(data.ClusterLightsCullCS, _clusterLightsCullKernel,ShaderIDs._LightsCullTexture, data.ClusterLightsCullResult.LightsCullTexture);
                    context.cmd.SetComputeBufferParam(data.ClusterLightsCullCS, _clusterLightsCullKernel, ShaderIDs._LightIndexBuffer, data.ClusterLightsCullResult.LightIndexBuffer);
                    context.cmd.SetComputeBufferParam(data.ClusterLightsCullCS, _clusterLightsCullKernel, ShaderIDs._LightStartOffsetCounter, data.LightStartOffsetCounter);
                    context.cmd.SetComputeBufferParam(data.ClusterLightsCullCS, _clusterLightsCullKernel, ShaderIDs._LightData, data.LightData);
                    context.cmd.DispatchCompute(data.ClusterLightsCullCS, _clusterLightsCullKernel, clustersNumX, clustersNumY, 1);
                });
            }
        }
    }
}