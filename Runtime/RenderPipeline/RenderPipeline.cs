using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HPipeline
{
    public struct RenderingData
    {
        internal CommandBuffer commandBuffer;
        public CullingResults cullResults;
        public Camera camera;
        public RenderTextureDescriptor cameraTargetDescriptor;
    }

    public sealed class RenderPipeline : UnityEngine.Rendering.RenderPipeline
    {
        internal static RenderGraph s_RenderGraph;

        private readonly RenderPipelineAsset pipelineAsset;

        FrameResources m_Resources = new FrameResources();

        DeferredLights m_DeferredLights;
        BuildLightGridPass m_BuildLightGridPass;
        GBufferPass m_GBufferPass;
        DeferredLightingPass m_DeferredLightingPass;
        DrawSkyboxPass m_DrawSkyboxPass;
        FinalBlitPass m_FinalBlitPass;

        RTHandle m_TargetHandle;
        //RTHandle m_DepthHandle;
        TextureHandle m_ActiveColorTexture;
        TextureHandle m_ActiveDepthTexture;

        // Materials used in URP Scriptable Render Passes
        Material m_BlitMaterial = null;
        Material m_DeferredLightingMaterial = null;

        public RenderPipeline(RenderPipelineAsset asset)
        {
            pipelineAsset = asset;

            RTHandles.Initialize(Screen.width, Screen.height);

            GraphicsSettings.useScriptableRenderPipelineBatching = true;

            s_RenderGraph = new RenderGraph("HPipeline");

            m_BlitMaterial = CoreUtils.CreateEngineMaterial("Hidden/Blit");
            m_DeferredLightingMaterial = new Material(Shader.Find("Hidden/DeferredLighting"));

            m_DeferredLights = new DeferredLights();
            m_BuildLightGridPass = new BuildLightGridPass(DeferredLights.MaxLights);
            m_GBufferPass = new GBufferPass();
            m_DeferredLightingPass = new DeferredLightingPass(m_DeferredLightingMaterial);
            m_DrawSkyboxPass = new DrawSkyboxPass();
            m_FinalBlitPass = new FinalBlitPass(m_BlitMaterial);

            EncodeBC6H.DefaultInstance = EncodeBC6H.DefaultInstance ?? new EncodeBC6H(Resources.Load<ComputeShader>("EncodeBC6H"));

            PreIntegratedBRDF.instance.Initialize();

            CubemapFilter.instance.Initialize();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            s_RenderGraph.Cleanup();
            s_RenderGraph = null;

            ConstantBuffer.ReleaseAll();

            m_DeferredLights?.Dispose();
            m_BuildLightGridPass?.Dispose();

            m_TargetHandle?.Release();
            //m_DepthHandle?.Release();

            PreIntegratedBRDF.instance.Cleanup();

            CoreUtils.Destroy(m_BlitMaterial);
            CoreUtils.Destroy(m_DeferredLightingMaterial);

            CubemapFilter.instance.Cleanup();
        }

#if UNITY_2021_1_OR_NEWER
        /// <inheritdoc/>
        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            Render(renderContext, new List<Camera>(cameras));
        }
#endif

#if UNITY_2021_1_OR_NEWER
        /// <inheritdoc/>
        protected override void Render(ScriptableRenderContext renderContext, List<Camera> cameras)
#else
        /// <inheritdoc/>
        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
#endif
        {
#if UNITY_2021_1_OR_NEWER
            BeginContextRendering(renderContext, cameras);
#else
            BeginFrameRendering(renderContext, cameras);
#endif
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
            GraphicsSettings.defaultRenderingLayerMask = 0x0001;
            SetupPerFrameShaderConstants();

            SortCameras(cameras);
            foreach (var camera in cameras)
            {
                BeginCameraRendering(renderContext, camera);

                RenderSingleCamera(renderContext, camera);

                EndCameraRendering(renderContext, camera);
            }

            s_RenderGraph.EndFrame();

#if UNITY_2021_1_OR_NEWER
            EndContextRendering(renderContext, cameras);
#else
            EndFrameRendering(renderContext, cameras);
#endif
        }

        /// <summary>
        /// Renders a single camera. This method will do culling, setup and execution of the renderer.
        /// </summary>
        void RenderSingleCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!camera.TryGetCullingParameters(false, out var cullingParameters))
                return;

            CommandBuffer cmd = CommandBufferPool.Get();

#if UNITY_EDITOR
            // Emit scene view UI
            if (camera.cameraType == CameraType.SceneView)
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            else
#endif
            if (camera.targetTexture != null && camera.cameraType != CameraType.Preview)
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);

            RTHandles.SetReferenceSize(camera.pixelWidth, camera.pixelHeight);

            var cullResults = context.Cull(ref cullingParameters);
            InitializeRenderingData(camera, ref cullResults, cmd, out var renderingData);

            //InitializeMainLightShadowResolution(ref renderingData.shadowData);

            PreIntegratedBRDF.instance.Render(cmd);

            RecordAndExecuteRenderGraph(s_RenderGraph, context, ref renderingData);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            context.Submit();
        }

        void SetupPerFrameShaderConstants()
        {
            Shader.SetGlobalVector(ShaderPropertyId.glossyEnvironmentCubeMapHDR, ReflectionProbe.defaultTextureHDRDecodeValues);
            Shader.SetGlobalTexture(ShaderPropertyId.glossyEnvironmentCubeMap, ReflectionProbe.defaultTexture);
        }

        Comparison<Camera> cameraComparison = (camera1, camera2) => { return (int)camera1.depth - (int)camera2.depth; };
#if UNITY_2021_1_OR_NEWER
        void SortCameras(List<Camera> cameras)
        {
            if (cameras.Count > 1)
                cameras.Sort(cameraComparison);
        }

#else
        void SortCameras(Camera[] cameras)
        {
            if (cameras.Length > 1)
                Array.Sort(cameras, cameraComparison);
        }
#endif

        void InitializeRenderingData(Camera camera, ref CullingResults cullResults, CommandBuffer cmd, out RenderingData renderingData)
        {
            renderingData.cullResults = cullResults;
            renderingData.camera = camera;
            renderingData.cameraTargetDescriptor = CreateRenderTextureDescriptor(camera);
            //InitializeShadowData(settings, visibleLights, mainLightCastShadows, additionalLightsCastShadows && !renderingData.lightData.shadeAdditionalLightsPerVertex, out renderingData.shadowData);
            //InitializePostProcessingData(settings, out renderingData.postProcessingData);
            renderingData.commandBuffer = cmd;
        }

        static RenderTextureDescriptor CreateRenderTextureDescriptor(Camera camera)
        {
            RenderTextureDescriptor desc;

            desc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
            desc.width = camera.pixelWidth;
            desc.height = camera.pixelHeight;
            desc.graphicsFormat = MakeRenderTextureGraphicsFormat();
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.sRGB = true;

            if (camera.targetTexture == null)
            {
                desc.graphicsFormat = MakeRenderTextureGraphicsFormat();
            }
            else
            {
                desc.graphicsFormat = camera.targetTexture.graphicsFormat;
            }

            // Make sure dimension is non zero
            desc.width = Mathf.Max(1, desc.width);
            desc.height = Mathf.Max(1, desc.height);

            desc.enableRandomWrite = false;
            desc.bindMS = false;
            desc.useDynamicScale = false;

            return desc;
        }

        static GraphicsFormat MakeRenderTextureGraphicsFormat()
        {
            if (RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Linear | FormatUsage.Render))
                return GraphicsFormat.B10G11R11_UFloatPack32;
            if (RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.Linear | FormatUsage.Render))
                return GraphicsFormat.R16G16B16A16_SFloat;
            return SystemInfo.GetGraphicsFormat(DefaultFormat.HDR); // This might actually be a LDR format on old devices.
        }

        void RecordAndExecuteRenderGraph(RenderGraph renderGraph, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = renderingData.commandBuffer;
            Camera camera = renderingData.camera;
            RenderGraphParameters rgParams = new RenderGraphParameters()
            {
                executionName = camera.name,
                commandBuffer = cmd,
                scriptableRenderContext = context,
                currentFrameIndex = Time.frameCount,
            };

            using (renderGraph.RecordAndExecute(rgParams))
            {
                RecordRenderGraph(renderGraph, context, ref renderingData);
            }
        }

        void RecordRenderGraph(RenderGraph renderGraph, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            m_Resources.InitFrame();

            InitRenderGraphFrame(renderGraph, ref renderingData);

            OnRecordRenderGraph(renderGraph, context, ref renderingData);
        }

        private class PassData
        {
            internal RenderingData renderingData;
        };

        void InitRenderGraphFrame(RenderGraph renderGraph, ref RenderingData renderingData)
        {
            using (var builder = renderGraph.AddLowLevelPass<PassData>("SetupLights", out var passData))
            {
                passData.renderingData = renderingData;

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, LowLevelGraphContext rgContext) =>
                {
                    m_DeferredLights.SetupLights(ref data.renderingData);

                    m_BuildLightGridPass.PrepareBuildLightGrid(m_DeferredLights, ref data.renderingData);
                });
            }
        }

        void OnRecordRenderGraph(RenderGraph renderGraph, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CreateRenderGraphCameraRenderTargets(renderGraph, ref renderingData);

            SetupRenderGraphCameraProperties(renderGraph, ref renderingData);

            OnBeforeRendering(renderGraph, ref renderingData);

            OnMainRendering(renderGraph, context, ref renderingData);

            OnAfterRendering(renderGraph, ref renderingData);
        }

        void CreateRenderGraphCameraRenderTargets(RenderGraph renderGraph, ref RenderingData renderingData)
        {
            Camera camera = renderingData.camera;
            RenderTargetIdentifier targetColorId = camera.targetTexture != null ? new RenderTargetIdentifier(camera.targetTexture) : BuiltinRenderTextureType.CameraTarget;
            m_Resources.SetTexture(FrameResourceType.BackBufferColor, renderGraph.ImportBackbuffer(targetColorId));

            CreateDepthTexture(renderGraph, renderingData.cameraTargetDescriptor);
        }

        void CreateDepthTexture(RenderGraph renderGraph, RenderTextureDescriptor descriptor)
        {
            var depthDescriptor = descriptor;
            depthDescriptor.msaaSamples = 1;// Depth-Only pass don't use MSAA
            depthDescriptor.graphicsFormat = GraphicsFormat.None;
            depthDescriptor.depthStencilFormat = GraphicsFormat.D32_SFloat;
            depthDescriptor.depthBufferBits = 32;

            m_ActiveDepthTexture = RenderingUtils.CreateRenderGraphTexture(renderGraph, depthDescriptor, "_ActiveDepthTexture", true);
        }

        void SetupRenderGraphCameraProperties(RenderGraph renderGraph, ref RenderingData renderingData)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("SetupCameraProperties", out var passData))
            {
                passData.renderingData = renderingData;

                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetupCameraProperties(data.renderingData.camera);
                    SetPerCameraShaderVariables(context.cmd, data.renderingData.camera);
                });
            }
        }

        void SetPerCameraShaderVariables(RasterCommandBuffer cmd, Camera camera)
        {
            float cameraWidth = (float)camera.pixelWidth;
            float cameraHeight = (float)camera.pixelHeight;

            cmd.SetGlobalVector(ShaderPropertyId.screenSize, new Vector4(cameraWidth, cameraHeight, 1.0f / cameraWidth, 1.0f / cameraHeight));

            // Set per camera matrices.
            SetCameraMatrices(cmd, camera);
        }

        void SetCameraMatrices(RasterCommandBuffer cmd, Camera camera)
        {
            Matrix4x4 view = camera.worldToCameraMatrix;
            Matrix4x4 proj = camera.projectionMatrix;
            Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(proj, false);

            // xy coordinates in range [-1; 1] go to pixel coordinates.
            Matrix4x4 toScreen = new Matrix4x4(
                new Vector4(0.5f * camera.pixelWidth, 0.0f, 0.0f, 0.0f),
                new Vector4(0.0f, 0.5f * camera.pixelHeight, 0.0f, 0.0f),
                new Vector4(0.0f, 0.0f, 1.0f, 0.0f),
                new Vector4(0.5f * camera.pixelWidth, 0.5f * camera.pixelHeight, 0.0f, 1.0f)
            );

            Matrix4x4 screenToWorld = Matrix4x4.Inverse(toScreen * gpuProj * view);
            cmd.SetGlobalMatrix(ShaderPropertyId.screenToWorld, screenToWorld);
        }

        void OnBeforeRendering(RenderGraph renderGraph, ref RenderingData renderingData)
        {
            //bool renderShadows = false;

            //if (m_MainLightShadowCasterPass.Setup(ref renderingData))
            //{
            //    renderShadows = true;
            //    TextureHandle mainShadowsTexture = m_MainLightShadowCasterPass.Render(renderGraph, ref renderingData);
            //    m_Resources.SetTexture(FrameResourceType.MainShadowsTexture, mainShadowsTexture);
            //}

            //if (m_AdditionalLightsShadowCasterPass.Setup(ref renderingData))
            //{
            //    renderShadows = true;
            //    TextureHandle additionalShadowsTexture = m_AdditionalLightsShadowCasterPass.Render(renderGraph, ref renderingData);
            //    m_Resources.SetTexture(FrameResourceType.AdditionalShadowsTexture, additionalShadowsTexture);
            //}

            //// The camera need to be setup again after the shadows since those passes override some settings
            //// TODO RENDERGRAPH: move the setup code into the shadow passes
            //if (renderShadows)
            //    SetupRenderGraphCameraProperties(renderGraph, ref renderingData);
        }

        void OnMainRendering(RenderGraph renderGraph, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            m_BuildLightGridPass.BuildLightGrid(renderGraph, out var clusterLightsCullResult);

            //TextureHandle internalColorLut;
            //m_PostProcessPasses.colorGradingLutPass.Render(renderGraph, out internalColorLut, ref renderingData);
            //m_Resources.SetTexture(FrameResourceType.InternalColorLut, internalColorLut);

            m_GBufferPass.Render(renderGraph, m_ActiveDepthTexture, ref renderingData, m_Resources);

            TextureHandle[] gbuffer = m_GBufferPass.GetFrameResourcesGBufferArray(m_Resources);
            m_DeferredLightingPass.Render(renderGraph, out m_ActiveColorTexture, m_ActiveDepthTexture, gbuffer, clusterLightsCullResult, ref renderingData);

            if (renderingData.camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
            {
                m_DrawSkyboxPass.Render(renderGraph, context, m_ActiveColorTexture, m_ActiveDepthTexture, ref renderingData);
            }
        }

        void OnAfterRendering(RenderGraph renderGraph, ref RenderingData renderingData)
        {
            DrawRenderGraphGizmos(renderGraph, m_ActiveColorTexture, m_ActiveDepthTexture, GizmoSubset.PreImageEffects, ref renderingData);

            TextureHandle backbuffer = m_Resources.GetTexture(FrameResourceType.BackBufferColor);

            //if (applyPostProcessing)
            //{
            //    TextureHandle internalColorLut = m_Resources.GetTexture(FrameResourceType.InternalColorLut);
            //    m_PostProcessPasses.postProcessPass.RenderPostProcessingRenderGraph(renderGraph, in m_ActiveColorTexture, in internalColorLut, in backbuffer, ref renderingData, false);
            //}
            //else
            {
                m_FinalBlitPass.Render(renderGraph, ref renderingData, m_ActiveColorTexture, backbuffer);
            }

            DrawRenderGraphGizmos(renderGraph, m_Resources.GetTexture(FrameResourceType.BackBufferColor), m_ActiveDepthTexture, GizmoSubset.PostImageEffects, ref renderingData);
        }

        class DrawGizmosPassData
        {
            public RendererListHandle gizmoRenderList;
        };

        /// <param name="color"></param>
        /// <param name="depth"></param>
        /// <param name="gizmoSubset"></param>
        /// <param name="renderingData"></param>
        internal void DrawRenderGraphGizmos(RenderGraph renderGraph, TextureHandle color, TextureHandle depth, GizmoSubset gizmoSubset, ref RenderingData renderingData)
        {
#if UNITY_EDITOR
            if (!Handles.ShouldRenderGizmos() || renderingData.camera.sceneViewFilterMode == Camera.SceneViewFilterMode.ShowFiltered)
                return;

            using (var builder = renderGraph.AddRasterRenderPass<DrawGizmosPassData>("Draw Gizmos Pass", out var passData))
            {
                builder.UseTextureFragment(color, 0, IBaseRenderGraphBuilder.AccessFlags.Write);
                builder.UseTextureFragmentDepth(depth, IBaseRenderGraphBuilder.AccessFlags.Read);

                passData.gizmoRenderList = renderGraph.CreateGizmoRendererList(renderingData.camera, gizmoSubset);
                builder.UseRendererList(passData.gizmoRenderList);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((DrawGizmosPassData data, RasterGraphContext rgContext) =>
                {
                    rgContext.cmd.DrawRendererList(data.gizmoRenderList);
                });
            }
#endif
        }
    }
}
