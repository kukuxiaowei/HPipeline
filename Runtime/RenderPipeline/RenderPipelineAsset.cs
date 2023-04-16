using UnityEngine;
using UnityEngine.Rendering;

namespace HPipeline
{
    public sealed class RenderPipelineAsset : RenderPipelineAsset<RenderPipeline>
    {
        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline()
            => new RenderPipeline(this);
    }
}
