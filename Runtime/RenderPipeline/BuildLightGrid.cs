using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HPipeline
{
    [GenerateHLSL]
    class LightGrid
    {
        public static int s_ClusterTileSize = 32;
        public static int s_ClusterDepth = 64;
        public static int s_ClusterMaxLight = 32;

        public static int s_ClusterPackingOffsetBits = 32 - (int)Mathf.Ceil(Mathf.Log(Mathf.NextPowerOfTwo(s_ClusterMaxLight), 2));
        public static int s_ClusterPackingOffsetMask = (1 << s_ClusterPackingOffsetBits) - 1;
    }

    struct BuildClusterLightGridResult
    {
        public BufferHandle clusterPackingOffset;
        public BufferHandle clusterLights;
    }

    internal class BuildLightGridPass
    {
        class BuildClusterLightGridPassData
        {
            public ComputeShader buildClusterLightGridCS;
            public BufferHandle globalLightGridCounter;
            public BufferHandle buildLightDatas;

            public BuildClusterLightGridResult result = new BuildClusterLightGridResult();
        }

        ComputeShader buildClusterLightGridCS;
        static int s_BuildClusterLightGridKernel;
        static int s_ClearGlobalCounterKernel;

        public GraphicsBuffer buildLightDatas { get; private set; }

        int m_ClusterX;
        int m_ClusterY;
        int m_BuildLightCount;

        internal BuildLightGridPass(int maxLights)
        {
            buildClusterLightGridCS = Resources.Load<ComputeShader>("BuildClusterLightGridCS");
            s_BuildClusterLightGridKernel = buildClusterLightGridCS.FindKernel("BuildClusterLightGrid");
            s_ClearGlobalCounterKernel = buildClusterLightGridCS.FindKernel("ClearGlobalCounter");

            buildLightDatas = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxLights, System.Runtime.InteropServices.Marshal.SizeOf(typeof(PunctualLightData)));
        }

        internal void Dispose()
        {
            CoreUtils.SafeRelease(buildLightDatas);
            buildLightDatas = null;
        }

        internal void PrepareBuildLightGrid(DeferredLights deferredLights, ref RenderingData renderingData)
        {
            var camera = renderingData.camera;
            m_ClusterX = Mathf.CeilToInt((float)camera.pixelWidth / LightGrid.s_ClusterTileSize);
            m_ClusterY = Mathf.CeilToInt((float)camera.pixelHeight / LightGrid.s_ClusterTileSize);

            float tanHalfFov = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float clusterCommonRatio = ClusterCommonRatio(m_ClusterY, tanHalfFov);
            float clusterZScale = ClusterZScale(camera.farClipPlane, camera.nearClipPlane, clusterCommonRatio);

            var cmd = renderingData.commandBuffer;
            cmd.SetGlobalInt(ShaderPropertyId.clusterNumTileX, m_ClusterX);
            cmd.SetGlobalInt(ShaderPropertyId.clusterNumTileY, m_ClusterY);
            cmd.SetGlobalVector(ShaderPropertyId.clusterZParams, new Vector4(clusterZScale, clusterCommonRatio, camera.nearClipPlane, 0.0f));
            cmd.SetGlobalVector(ShaderPropertyId.clipToViewParams, new Vector4(tanHalfFov * camera.aspect, tanHalfFov, 0.0f, 0.0f));

            var viewMatrix = camera.worldToCameraMatrix;
            // camera.worldToCameraMatrix is RHS and Unity's transforms are LHS, we need to flip it to work with transforms.
            // Note that this is equivalent to s_FlipMatrixLHSRHS * viewMatrix, but faster given that it doesn't need full matrix multiply
            // However if for some reason s_FlipMatrixLHSRHS changes from Matrix4x4.Scale(new Vector3(1, 1, -1)), this need to change as well.
            viewMatrix.m20 *= -1;
            viewMatrix.m21 *= -1;
            viewMatrix.m22 *= -1;
            viewMatrix.m23 *= -1;

            var lightBounds = deferredLights.LightBounds;
            m_BuildLightCount = lightBounds.size;
            for (int i = 0; i < m_BuildLightCount; i++)
            {
                ref LightBound lightBound = ref lightBounds[i];
                lightBound.position = viewMatrix.MultiplyPoint(lightBound.position);
                lightBound.forward = viewMatrix.MultiplyVector(lightBound.forward);
            }
            buildLightDatas.SetData(lightBounds, 0, 0, m_BuildLightCount);
        }

        internal void BuildLightGrid(RenderGraph renderGraph, out BuildClusterLightGridResult result)
        {
            using (var builder = renderGraph.AddRenderPass<BuildClusterLightGridPassData>("Build Light Grid", out var passData))
            {
                builder.EnableAsyncCompute(true);

                m_ClusterX = Mathf.Max(1, m_ClusterX);
                m_ClusterY = Mathf.Max(1, m_ClusterY);
                int clustersCount = m_ClusterX * m_ClusterY * LightGrid.s_ClusterDepth;

                passData.buildClusterLightGridCS = this.buildClusterLightGridCS;
                passData.result.clusterPackingOffset = builder.WriteBuffer(renderGraph.CreateBuffer(new BufferDesc((int)LightCategory.Count * clustersCount, sizeof(uint)) { name = "ClusterPackingOffset" }));
                passData.result.clusterLights = builder.WriteBuffer(renderGraph.CreateBuffer(new BufferDesc(LightGrid.s_ClusterMaxLight * clustersCount, sizeof(uint)) { name = "ClusterLights" }));

                passData.globalLightGridCounter = builder.CreateTransientBuffer(new BufferDesc(1, sizeof(uint)) { name = "GlobalLightGridCounter" });

                passData.buildLightDatas = builder.ReadBuffer(renderGraph.ImportBuffer(buildLightDatas));

                builder.SetRenderFunc((BuildClusterLightGridPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeBufferParam(data.buildClusterLightGridCS, s_ClearGlobalCounterKernel, ShaderPropertyId.globalLightGridCounter, data.globalLightGridCounter);
                    context.cmd.DispatchCompute(data.buildClusterLightGridCS, s_ClearGlobalCounterKernel, 1, 1, 1);

                    context.cmd.SetComputeBufferParam(data.buildClusterLightGridCS, s_BuildClusterLightGridKernel, ShaderPropertyId.clusterPackingOffset, data.result.clusterPackingOffset);
                    context.cmd.SetComputeBufferParam(data.buildClusterLightGridCS, s_BuildClusterLightGridKernel, ShaderPropertyId.clusterLights, data.result.clusterLights);
                    context.cmd.SetComputeBufferParam(data.buildClusterLightGridCS, s_BuildClusterLightGridKernel, ShaderPropertyId.globalLightGridCounter, data.globalLightGridCounter);
                    context.cmd.SetComputeBufferParam(data.buildClusterLightGridCS, s_BuildClusterLightGridKernel, ShaderPropertyId.buildLightDatas, data.buildLightDatas);
                    context.cmd.SetComputeIntParam(data.buildClusterLightGridCS, ShaderPropertyId.buildLightCount, m_BuildLightCount);
                    context.cmd.DispatchCompute(data.buildClusterLightGridCS, s_BuildClusterLightGridKernel, m_ClusterX, m_ClusterY, 1);
                });

                result = passData.result;
            }
        }

        // "Clustered Deferred and Forward Shading"
        float ClusterCommonRatio(int clustersY, float tanHalfFov)
        {
            return 1.0f + 2.0f * tanHalfFov / (float)clustersY;
        }

        float ClusterZScale(float far, float near, float commonRatio)
        {
            // Sum of Geometric Progression
            float sum = (Mathf.Pow(commonRatio, LightGrid.s_ClusterDepth) - 1.0f) / (commonRatio - 1.0f);
            return sum / (far - near);
        }
    }
}