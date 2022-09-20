using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HPipeline
{
    public partial class RenderPipeline : UnityEngine.Rendering.RenderPipeline
    {
        RenderGraph m_RenderGraph = new RenderGraph("HPipeline");

        public RenderPipeline(RenderPipelineAsset asset)
        {
            GraphicsSettings.useScriptableRenderPipelineBatching = true;

            RTHandles.Initialize(Screen.width, Screen.height);

            IBL.instance.ProbesDataInit();
            LightDataInit();
            ClusterLightsCullPassInit();
            DeferredLightingPassInit();
            FinalBlitPassInit();
        }

#if UNITY_2021_1_OR_NEWER
        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            Render(renderContext, new List<Camera>(cameras));
        }
#endif

#if UNITY_2021_1_OR_NEWER
        protected override void Render(ScriptableRenderContext renderContext, List<Camera> cameras)
#else
        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
#endif
        {
            var camera = cameras[0];
            if (camera == null) 
                return;
            CullingResults cullingResults = default;
            if (!TryCull(camera, renderContext, out var cullingParams, ref cullingResults)) 
                return;

            /*foreach(var camera in cameras)
            {
                if(camera == null)
                    continue;

                CullingResults cullingResults = default;
                if(!TryCull(camera, renderContext, out var cullingParams, ref cullingResults))
                    continue;
            }*/

            var pixelRect = camera.pixelRect;
            RTHandles.SetReferenceSize((int)pixelRect.size.x, (int)pixelRect.size.y);
            RenderTargetIdentifier cID = BuiltinRenderTextureType.CameraTarget;
            
            var cmd = CommandBufferPool.Get("");

            //Light
            LightDataSetup(cmd, cullingResults, out var lightData);

            //Camera
            renderContext.SetupCameraProperties(camera);
            cmd.SetGlobalMatrix(ShaderIDs._ScreenToWorldMatrix, (camera.projectionMatrix * camera.worldToCameraMatrix).inverse);
            cmd.SetGlobalMatrix(ShaderIDs._WorldToViewMatrix, camera.worldToCameraMatrix);
            cmd.SetGlobalVector(ShaderIDs._CameraData, new Vector4(camera.nearClipPlane, camera.farClipPlane));
            
            //IBL
            IBL.instance.ProbesDataSetup();

#region RenderGraph
            var renderGraphParams = new RenderGraphParameters()
            {
                scriptableRenderContext = renderContext,
                commandBuffer = cmd,
                //currentFrameIndex = frameIndex
            };

            using (m_RenderGraph.RecordAndExecute(renderGraphParams))
            {
                var backBuffer = m_RenderGraph.ImportBackbuffer(cID);

                //Pass
                GBufferPassExecute(m_RenderGraph, cullingResults, camera, out var gBuffer);
                ClusterLightsCullPassExecute(m_RenderGraph, camera, lightData, out var clusterLightsCullResult);
                DeferredLightingPassExecute(m_RenderGraph, gBuffer, clusterLightsCullResult, out var colorBuffer);
                FinalBlitPassExecute(m_RenderGraph, camera, colorBuffer, backBuffer);
            }
#endregion

            renderContext.ExecuteCommandBuffer(cmd);
            renderContext.Submit();
            CommandBufferPool.Release(cmd);

            m_RenderGraph.EndFrame();
        }

        private static bool TryCull(
            Camera camera,
            ScriptableRenderContext renderContext,
            out ScriptableCullingParameters cullingParams,
            ref CullingResults cullingResults
        )
        {
            cullingParams = default;
            if (!camera.TryGetCullingParameters(camera.stereoEnabled, out cullingParams))
                return false;
            
            cullingResults = renderContext.Cull(ref cullingParams);

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            IBL.instance.ProbesDataCleanup();
            LightDataCleanup();
            DeferredLightingPassDispose();
            FinalBlitPassDispose();

            CleanupRenderGraph();
        }

        private void CleanupRenderGraph()
        {
            m_RenderGraph.Cleanup();
            m_RenderGraph = null;
        }


        private static void Destroy(Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
#else
                Object.Destroy(obj);
#endif
        }
    }
}
