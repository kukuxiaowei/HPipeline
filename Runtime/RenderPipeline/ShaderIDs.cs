using UnityEngine;
using UnityEngine.Rendering;

namespace HPipeline
{
    internal static class ShaderIDs
    {
        public static readonly ShaderTagId GBuffer = new("GBuffer");
        
        public static readonly int _DepthBuffer = Shader.PropertyToID("_DepthBuffer");
        public static readonly int _GBuffer0 = Shader.PropertyToID("_GBuffer0");
        public static readonly int _GBuffer1 = Shader.PropertyToID("_GBuffer1");
        public static readonly int _GBuffer2 = Shader.PropertyToID("_GBuffer2");
        public static readonly int _LightsCullTexture = Shader.PropertyToID("_LightsCullTexture");
        public static readonly int _LightIndexBuffer = Shader.PropertyToID("_LightIndexBuffer");
        public static readonly int _LightData = Shader.PropertyToID("_LightData");
    }
}