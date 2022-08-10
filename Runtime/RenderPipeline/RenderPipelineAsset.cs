using UnityEngine;
using UnityEngine.Rendering;

namespace HPipeline
{
    public sealed class RenderPipelineAsset : UnityEngine.Rendering.RenderPipelineAsset
    {
        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline()
            => new RenderPipeline(this);
    }
}
