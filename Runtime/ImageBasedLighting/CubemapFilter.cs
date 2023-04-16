using UnityEngine;
using UnityEngine.Rendering;

namespace HPipeline
{
    public class CubemapFilter
    {
        const int k_MipCount = 7;

        static CubemapFilter s_Instance;
        public static CubemapFilter instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new CubemapFilter();

                return s_Instance;
            }
        }

        Material m_Material;
        MaterialPropertyBlock m_MaterialPropertyBlock = new MaterialPropertyBlock();

        public void Initialize()
        {
            m_Material = CoreUtils.CreateEngineMaterial("Hidden/CubemapFilter");
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(m_Material);
        }

        public void Filter(CommandBuffer cmd, Texture source, RenderTexture target)
        {
            // Copy the first mip
            for (int f = 0; f < 6; f++)
            {
                cmd.CopyTexture(source, f, 0, target, f, 0);
            }

            m_MaterialPropertyBlock.SetTexture("_CubeMap", source);

            for (int mip = 1; mip < k_MipCount; ++mip)
            {
                m_MaterialPropertyBlock.SetFloat(ShaderPropertyId.mipLevel, mip);

                for (int face = 0; face < 6; ++face)
                {
                    m_MaterialPropertyBlock.SetInt(ShaderPropertyId.faceIndex, face);

                    CoreUtils.SetRenderTarget(cmd, target, ClearFlag.None, mip, (CubemapFace)face);
                    CoreUtils.DrawFullScreen(cmd, m_Material, m_MaterialPropertyBlock);
                }
            }
        }
    }
}