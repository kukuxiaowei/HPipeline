using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HPipeline
{
    public sealed class RenderPipeline : UnityEngine.Rendering.RenderPipeline
    {
        RenderGraph m_RenderGraph = new RenderGraph("HPipeline");

        public RenderPipeline(RenderPipelineAsset asset)
        {

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
            foreach(var camera in cameras)
            {
                if(camera == null)
                    continue;

                CullingResults cullingResults = default;
                if(!TryCull(camera, renderContext, out var cullingParams, ref cullingResults))
                    continue;

                
            }
            
            var cmd = CommandBufferPool.Get("");
#region RenderGraph
            var renderGraphParams = new RenderGraphParameters()
            {
                scriptableRenderContext = renderContext,
                commandBuffer = cmd,
                //currentFrameIndex = frameIndex
            };

            using (m_RenderGraph.RecordAndExecute(renderGraphParams))
            {
                // Add your passes here
            }

            m_RenderGraph.EndFrame();
#endregion
        }

        static bool TryCull(
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

            CleanupRenderGraph();
        }

        void CleanupRenderGraph()
        {
            m_RenderGraph.Cleanup();
            m_RenderGraph = null;
        }
    }
}
