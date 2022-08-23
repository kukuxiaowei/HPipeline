using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace HPipeline
{
    public partial class RenderPipeline
    {
        private const int clusterSizeX = 32;
        private const int clusterSizeY = 32;
        private const int clustersNumZ = 16;

        private ComputeShader _clusterLightsCullShader;
        private int _kernel;
        
        void ClusterLightsCullPassInit()
        {
            _clusterLightsCullShader = Resources.Load<ComputeShader>("ClusterLightsCullShader");
            _kernel = _clusterLightsCullShader.FindKernel("ClusterLightsCull");
        }
        
        class ClusterLightsCullPassData
        {
            public int ClustersNumX;
            public int ClustersNumY;
            public ComputeShader ClusterLightsCullShader;
            public int Kernel;
            public ComputeBufferHandle LightIndexStart;
            public ComputeBufferHandle LightIndexLength;
        }
        
        void ClusterLightsCullPassExecute(RenderGraph renderGraph, CullingResults cullingResults, Camera camera)
        {
            using (var builder = renderGraph.AddRenderPass<ClusterLightsCullPassData>("ClusterLightsCull Pass", out var passData))
            {
                builder.EnableAsyncCompute(true);

                var pixelRect = camera.pixelRect;
                int clustersNumX = Mathf.CeilToInt(pixelRect.x / clusterSizeX);
                int clustersNumY = Mathf.CeilToInt(pixelRect.y / clusterSizeY);

                passData.ClustersNumX = clustersNumX;
                passData.ClustersNumY = clustersNumY;
                passData.ClusterLightsCullShader = _clusterLightsCullShader;
                passData.Kernel = _kernel;
                passData.LightIndexStart = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(
                    new ComputeBufferDesc(clustersNumX * clustersNumY * clustersNumZ, sizeof(uint))
                        { name = "LightIndexStart" }));
                passData.LightIndexLength = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(
                    new ComputeBufferDesc(clustersNumX * clustersNumY * clustersNumZ, sizeof(uint))
                        { name = "LightIndexLength" }));
                
                builder.SetRenderFunc((ClusterLightsCullPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeBufferParam(data.ClusterLightsCullShader, data.Kernel,
                        ShaderIDs._LightIndexStart, data.LightIndexStart);
                    context.cmd.SetComputeBufferParam(data.ClusterLightsCullShader, data.Kernel,
                        ShaderIDs._LightIndexLength, data.LightIndexLength);
                    context.cmd.DispatchCompute(data.ClusterLightsCullShader, data.Kernel, passData.ClustersNumX, passData.ClustersNumY, 1);
                });
            }
        }
    }
}