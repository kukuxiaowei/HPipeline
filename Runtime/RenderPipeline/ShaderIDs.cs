using UnityEngine;
using UnityEngine.Rendering;

namespace HPipeline
{
    internal static class ShaderIDs
    {
        //ShaderTagId
        public static readonly ShaderTagId GBuffer = new("GBuffer");
        
        //RenderTexture
        public static readonly int _DepthBuffer = Shader.PropertyToID("_DepthBuffer");
        public static readonly int _GBuffer0 = Shader.PropertyToID("_GBuffer0");
        public static readonly int _GBuffer1 = Shader.PropertyToID("_GBuffer1");
        public static readonly int _GBuffer2 = Shader.PropertyToID("_GBuffer2");
        public static readonly int _BakedGI = Shader.PropertyToID("_BakedGI");
        public static readonly int _Source = Shader.PropertyToID("_Source");

        //CB
        public static readonly int _CameraData = Shader.PropertyToID("_CameraData");
        public static readonly int _BlitScaleOffset = Shader.PropertyToID("_BlitScaleOffset");

        //Light
        public static readonly int _MainLightPosition = Shader.PropertyToID("_MainLightPosition");
        public static readonly int _MainLightColor = Shader.PropertyToID("_MainLightColor");
        public static readonly int _LightData = Shader.PropertyToID("_LightData");
        public static readonly int _LightCount = Shader.PropertyToID("_LightCount");

        //Cluster
        public static readonly int _LightsCullTexture = Shader.PropertyToID("_LightsCullTexture");
        public static readonly int _LightIndexBuffer = Shader.PropertyToID("_LightIndexBuffer");
        public static readonly int _LightStartOffsetCounter = Shader.PropertyToID("_LightStartOffsetCounter");
        public static readonly int _ClustersNumData = Shader.PropertyToID("_ClustersNumData");
        public static readonly int _ClusterSizeData = Shader.PropertyToID("_ClusterSizeData");

        //Matrix
        public static readonly int _ScreenToWorldMatrix = Shader.PropertyToID("_ScreenToWorldMatrix");
        public static readonly int _WorldToViewMatrix = Shader.PropertyToID("_WorldToViewMatrix");
        
        //IBL
        public static readonly int _IntegratedBRDFTexture = Shader.PropertyToID("_IntegratedBRDFTexture");
        public static readonly int _ProbesCount = Shader.PropertyToID("_ProbesCount");
        public static readonly int _ProbesTexture = Shader.PropertyToID("_ProbesTexture");
        public static readonly int _LOD = Shader.PropertyToID("_LOD");
        public static readonly int _Face = Shader.PropertyToID("_Face");
    }
}