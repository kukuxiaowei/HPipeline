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
            RTHandles.Initialize(Screen.width, Screen.height);

            CreateMaterial();
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
            renderContext.SetupCameraProperties(camera);

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

                //SetCBuffer
                cmd.SetGlobalVector("_MainLightPosition", -cullingResults.visibleLights[0].localToWorldMatrix.GetColumn(2));
                cmd.SetGlobalMatrix("_ScreenToWorldMatrix", (camera.projectionMatrix * camera.worldToCameraMatrix).inverse);

                //Pass
                AddGBufferPass(m_RenderGraph, cullingResults, camera, out var gBuffer);
                AddDeferredLightingPass(m_RenderGraph, gBuffer, out var colorBuffer);
                AddFinalBlitPass(m_RenderGraph, colorBuffer, backBuffer);
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
            
            DestroyMaterial();

            CleanupRenderGraph();
        }

        private void CleanupRenderGraph()
        {
            m_RenderGraph.Cleanup();
            m_RenderGraph = null;
        }
        
#region Material

        private Material _deferredLightingMaterial;
        private Material _blitMaterial;

        private void CreateMaterial()
        {
            _deferredLightingMaterial = new Material(Shader.Find("Hidden/DeferredLighting"));
            _blitMaterial = new Material(Shader.Find("Hidden/Blit"));
        }

        private void DestroyMaterial()
        {
            Destroy(_deferredLightingMaterial);
            Destroy(_blitMaterial);
        }

        private Material GetBlitMaterial()
        {
            return _blitMaterial;
        }

        private Material GetDeferredLightingMaterial()
        {
            return _deferredLightingMaterial;
        }
#endregion

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
