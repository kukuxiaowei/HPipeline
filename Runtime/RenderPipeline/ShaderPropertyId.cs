using UnityEngine;

namespace HPipeline
{
    internal static class ShaderPropertyId
    {
        public static readonly int glossyEnvironmentCubeMap = Shader.PropertyToID("_GlossyEnvironmentCubeMap");
        public static readonly int glossyEnvironmentCubeMapHDR = Shader.PropertyToID("_GlossyEnvironmentCubeMap_HDR");

        // BuiltinShaderVariables https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html
        public static readonly int worldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
        public static readonly int screenParams = Shader.PropertyToID("_ScreenParams");
        public static readonly int projectionParams = Shader.PropertyToID("_ProjectionParams");
        public static readonly int zBufferParams = Shader.PropertyToID("_ZBufferParams");
        public static readonly int orthoParams = Shader.PropertyToID("unity_OrthoParams");

        public static readonly int viewMatrix = Shader.PropertyToID("unity_MatrixV");
        public static readonly int projectionMatrix = Shader.PropertyToID("glstate_matrix_projection");
        public static readonly int viewAndProjectionMatrix = Shader.PropertyToID("unity_MatrixVP");
        public static readonly int inverseViewMatrix = Shader.PropertyToID("unity_MatrixInvV");
        public static readonly int cameraProjectionMatrix = Shader.PropertyToID("unity_CameraProjection");
        public static readonly int inverseCameraProjectionMatrix = Shader.PropertyToID("unity_CameraInvProjection");
        public static readonly int worldToCameraMatrix = Shader.PropertyToID("unity_WorldToCamera");
        public static readonly int cameraToWorldMatrix = Shader.PropertyToID("unity_CameraToWorld");
        // BuiltinShaderVariables

        public static readonly int screenSize = Shader.PropertyToID("_ScreenSize");

        // DeferredLights
        public static readonly int directionalLightCount = Shader.PropertyToID("_DirectionalLightCount");
        public static readonly int directionalLightDatas = Shader.PropertyToID("_DirectionalLightDatas");
        public static readonly int punctualLightDatas = Shader.PropertyToID("_PunctualLightDatas");
        public static readonly int envLightDatas = Shader.PropertyToID("_EnvLightDatas"); 

        // GBuffer
        public static readonly int depthBuffer = Shader.PropertyToID("_DepthBuffer");
        public static readonly int gBuffer0 = Shader.PropertyToID("_GBuffer0");
        public static readonly int gBuffer1 = Shader.PropertyToID("_GBuffer1");
        public static readonly int gBuffer2 = Shader.PropertyToID("_GBuffer2");
        public static readonly int bakedGI = Shader.PropertyToID("_BakedGI");

        // DeferredLighting
        public static readonly int screenToWorld = Shader.PropertyToID("_ScreenToWorld");

        // Cluster
        public static readonly int buildLightCount = Shader.PropertyToID("_BuildLightCount");
        public static readonly int buildLightDatas = Shader.PropertyToID("_BuildLightDatas");
        public static readonly int clusterPackingOffset = Shader.PropertyToID("_ClusterPackingOffset");
        public static readonly int clusterLights = Shader.PropertyToID("_ClusterLights");
        public static readonly int globalLightGridCounter = Shader.PropertyToID("_GlobalLightGridCounter");
        public static readonly int clusterNumTileX = Shader.PropertyToID("_ClusterNumTileX");
        public static readonly int clusterNumTileY = Shader.PropertyToID("_ClusterNumTileY");
        public static readonly int clipToViewParams = Shader.PropertyToID("_ClipToViewParams");
        public static readonly int clusterZParams = Shader.PropertyToID("_ClusterZParams");

        // ImageBasedLighting
        public static readonly int preIntegratedBRDF = Shader.PropertyToID("_PreIntegratedBRDF");
        public static readonly int envCubemapArray = Shader.PropertyToID("_EnvCubemapArray");
        public static readonly int inputTex = Shader.PropertyToID("_InputTex");
        public static readonly int LOD = Shader.PropertyToID("_LoD");
        public static readonly int faceIndex = Shader.PropertyToID("_FaceIndex");
        public static readonly int mipLevel = Shader.PropertyToID("_MipLevel");

        // Blit
        public static readonly int blitTexture = Shader.PropertyToID("_BlitTexture");
        public static readonly int blitScaleBias = Shader.PropertyToID("_BlitScaleBias");
    }
}