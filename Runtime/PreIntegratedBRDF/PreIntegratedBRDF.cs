using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace HPipeline
{
    public class PreIntegratedBRDF
    {
        [GenerateHLSL]
        public enum PreIntegratedTexture
        {
            Resolution = 64
        }

        static PreIntegratedBRDF s_Instance;
        public static PreIntegratedBRDF instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new PreIntegratedBRDF();

                return s_Instance;
            }
        }

        bool m_HasBuild = false;
        Material m_Material;
        RenderTexture m_Texture;

        public void Initialize()
        {
            int resolution = (int) PreIntegratedTexture.Resolution;
            m_Material = CoreUtils.CreateEngineMaterial("Hidden/PreIntegratedBRDF");
            m_Texture = new RenderTexture(resolution, resolution, 0, GraphicsFormat.B10G11R11_UFloatPack32);
            m_Texture.hideFlags = HideFlags.HideAndDontSave;
            m_Texture.filterMode = FilterMode.Bilinear;
            m_Texture.wrapMode = TextureWrapMode.Clamp;
            m_Texture.name = "PreIntegratedBRDF";
            m_Texture.Create();

            m_HasBuild = false;
        }

        public void Render(CommandBuffer cmd)
        {
            if (false == m_HasBuild)
            {
                CoreUtils.DrawFullScreen(cmd, m_Material, new RenderTargetIdentifier(m_Texture));
                m_HasBuild = true;
            }

            cmd.SetGlobalTexture(ShaderPropertyId.preIntegratedBRDF, m_Texture);
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(m_Material);
            CoreUtils.Destroy(m_Texture);

            m_HasBuild = false;
        }
    }
}