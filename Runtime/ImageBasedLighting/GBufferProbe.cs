using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace HPipeline
{
    [RequireComponent(typeof(ReflectionProbe))]
    [ExecuteInEditMode]
    public class GBufferProbe : MonoBehaviour
    {
        public Cubemap albedoCubemap;
        public Cubemap normalCubemap;
        public Cubemap mixCubemap;

        RenderTexture albedoRT;
        RenderTexture normalRT;
        RenderTexture mixRT;

        private void OnDisable()
        {
            albedoCubemap = null;
            normalCubemap = null;
            mixCubemap = null;
            albedoRT = null;
            normalRT = null;
            mixRT = null;
        }

        void Update()
        {
            if (albedoCubemap == null)
            {
                albedoCubemap = new Cubemap(128, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None);
                albedoCubemap.Apply();

                albedoRT = new RenderTexture(128, 128, 0, GraphicsFormat.R8G8B8A8_SRGB);
                albedoRT.Create();
            }
            if (normalCubemap == null)
            {
                normalCubemap = new Cubemap(128, GraphicsFormat.R16G16B16A16_UNorm, TextureCreationFlags.None);
                normalCubemap.Apply();

                normalRT = new RenderTexture(128, 128, 0, GraphicsFormat.R16G16B16A16_UNorm);
                normalRT.Create();
            }
            if (mixCubemap == null)
            {
                mixCubemap = new Cubemap(128, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
                mixCubemap.Apply();

                mixRT = new RenderTexture(128, 128, 0, GraphicsFormat.R8G8B8A8_UNorm);
                mixRT.Create();
            }
        }

        public void SetFrameResourcesGBufferArray(RenderGraph renderGraph, FrameResources frameResources)
        {
            var gbufferTextureHandles = new TextureHandle[3];
            gbufferTextureHandles[0] = renderGraph.ImportTexture(RTHandles.Alloc(normalRT));
            gbufferTextureHandles[1] = renderGraph.ImportTexture(RTHandles.Alloc(albedoRT));
            gbufferTextureHandles[2] = renderGraph.ImportTexture(RTHandles.Alloc(mixRT));
            frameResources.SetFrameResourcesGBufferArray(gbufferTextureHandles);
        }

        static readonly Vector3[] CameraEulerAngles =
        {
            new Vector3(0, 90, 0),
            new Vector3(0, -90, 0),
            new Vector3(-90, 0, 0),
            new Vector3(90, 0, 0),
            new Vector3(0, 0, 0),
            new Vector3(0, 180, 0),
        };

        public void CopyRenderTextureToCubemap(RenderTexture rt, Texture cube, int face)
        {
            RenderTexture.active = rt;
            Texture2D temp = new Texture2D(rt.width, rt.height, rt.graphicsFormat, TextureCreationFlags.None);
            temp.ReadPixels(new Rect(0, 0, temp.width, temp.height), 0, 0);
            temp.Apply();
            Graphics.CopyTexture(temp, 0, cube, face);
        }

        public void BakeProbe()
        {
            var reflectionProbe = this.GetComponent<ReflectionProbe>();

            var go = new GameObject("GBuffer Probe Capture Camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = this.gameObject.transform.position;
            camera.transform.rotation = Quaternion.identity;
            camera.fieldOfView = 90.0f;
            camera.aspect = 1.0f;
            camera.orthographic = false;
            camera.nearClipPlane = reflectionProbe.nearClipPlane;
            camera.farClipPlane = reflectionProbe.farClipPlane;

            var cameraData = go.AddComponent<AdditionalCameraData>();
            cameraData.probe = this;
            camera.targetTexture = new RenderTexture(new RenderTextureDescriptor(128, 128, RenderTextureFormat.ARGB32));
            camera.cameraType = CameraType.Reflection;

            Quaternion[] faceRots = new Quaternion[6];
            for (int i = 0; i < 6; ++i)
            {
                faceRots[i] = Quaternion.Euler(CameraEulerAngles[i]);
            }

            for (int i = 0; i < faceRots.Length; i++)
            {
                camera.transform.rotation = faceRots[i];
                camera.Render();

                CopyRenderTextureToCubemap(albedoRT, albedoCubemap, i);
                CopyRenderTextureToCubemap(normalRT, normalCubemap, i);
                CopyRenderTextureToCubemap(mixRT,    mixCubemap,    i);
            }

            camera.targetTexture = null;
            GameObject.DestroyImmediate(camera.gameObject);
        }
    }
}