using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace HPipeline
{
    class ReflectionProbeCache
    {
        int m_ProbeSize;
        int m_CacheSize;

        TextureCacheCubemap m_TextureCache;
        RenderTexture m_TempRenderTexture;
        RenderTexture m_ConvolutionTargetTexture;
        Material m_ConvertTextureMaterial;
        Material m_CubeToPano;
        MaterialPropertyBlock m_ConvertTextureMPB;
        bool m_PerformBC6HCompression;

        GraphicsFormat m_ProbeFormat;

        bool m_HasRelight;
        Texture[] m_PrepareRelightTextures;

        public ReflectionProbeCache(int cacheSize, int probeSize, GraphicsFormat probeFormat, bool isMipmaped)
        {
            m_ConvertTextureMaterial = CoreUtils.CreateEngineMaterial("Hidden/BlitCubeTextureFace");
            m_ConvertTextureMPB = new MaterialPropertyBlock();
            m_CubeToPano = CoreUtils.CreateEngineMaterial("Hidden/CubeToPano");

            Debug.Assert(probeFormat == GraphicsFormat.RGB_BC6H_UFloat || probeFormat == GraphicsFormat.B10G11R11_UFloatPack32 || probeFormat == GraphicsFormat.R16G16B16A16_SFloat,
                "Reflection Probe Cache format for HDRP can only be BC6H, FP16 or R11G11B10.");
            m_ProbeFormat = probeFormat;

            m_ProbeSize = probeSize;
            m_CacheSize = cacheSize;
            m_TextureCache = new TextureCacheCubemap("ReflectionProbe");
            m_TextureCache.AllocTextureArray(cacheSize, probeSize, probeFormat, isMipmaped, m_CubeToPano);

            m_PerformBC6HCompression = probeFormat == GraphicsFormat.RGB_BC6H_UFloat;

            m_PrepareRelightTextures = new Texture[cacheSize];
        }

        void Initialize()
        {
            if (m_TempRenderTexture == null)
            {
                // Temporary RT used for convolution and compression
                m_TempRenderTexture = new RenderTexture(m_ProbeSize, m_ProbeSize, 1, m_ProbeFormat);
                m_TempRenderTexture.hideFlags = HideFlags.HideAndDontSave;
                m_TempRenderTexture.dimension = TextureDimension.Cube;
                m_TempRenderTexture.useMipMap = true;
                m_TempRenderTexture.autoGenerateMips = false;
                m_TempRenderTexture.name = CoreUtils.GetRenderTargetAutoName(m_ProbeSize, m_ProbeSize, 1, m_ProbeFormat, "ReflectionProbeTemp", mips: true);
                m_TempRenderTexture.Create();

                {
                    m_ConvolutionTargetTexture = new RenderTexture(m_ProbeSize, m_ProbeSize, 1, m_ProbeFormat);
                    m_ConvolutionTargetTexture.hideFlags = HideFlags.HideAndDontSave;
                    m_ConvolutionTargetTexture.dimension = TextureDimension.Cube;
                    m_ConvolutionTargetTexture.useMipMap = true;
                    m_ConvolutionTargetTexture.autoGenerateMips = false;
                    m_ConvolutionTargetTexture.name = CoreUtils.GetRenderTargetAutoName(m_ProbeSize, m_ProbeSize, 1, m_ProbeFormat, "ReflectionProbeConvolution", mips: true);
                    m_ConvolutionTargetTexture.Create();
                }
            }
        }

        public void Release()
        {
            m_TextureCache.Release();
            CoreUtils.Destroy(m_TempRenderTexture);

            if (m_ConvolutionTargetTexture != null)
            {
                CoreUtils.Destroy(m_ConvolutionTargetTexture);
                m_ConvolutionTargetTexture = null;
            }

            CoreUtils.Destroy(m_ConvertTextureMaterial);
            CoreUtils.Destroy(m_CubeToPano);
        }

        public void NewFrame()
        {
            Initialize();
            m_TextureCache.NewFrame();
            m_HasRelight = false;
        }

        // This method is used to convert inputs that are either compressed or not of the right size.
        // We can't use Graphics.ConvertTexture here because it does not work with a RenderTexture as destination.
        void ConvertTexture(CommandBuffer cmd, Texture input, RenderTexture target)
        {
            m_ConvertTextureMPB.SetTexture(ShaderPropertyId.inputTex, input);
            m_ConvertTextureMPB.SetFloat(ShaderPropertyId.LOD, 0.0f); // We want to convert mip 0 to whatever the size of the destination cache is.
            for (int f = 0; f < 6; ++f)
            {
                m_ConvertTextureMPB.SetFloat(ShaderPropertyId.faceIndex, (float)f);
                CoreUtils.SetRenderTarget(cmd, target, ClearFlag.None, Color.black, 0, (CubemapFace)f);
                CoreUtils.DrawFullScreen(cmd, m_ConvertTextureMaterial, m_ConvertTextureMPB);
            }
        }

        Texture ConvolveProbeTexture(CommandBuffer cmd, Texture texture)
        {
            // Probes can be either Cubemaps (for baked probes) or RenderTextures (for realtime probes)
            Cubemap cubeTexture = texture as Cubemap;
            RenderTexture renderTexture = texture as RenderTexture;

            RenderTexture convolutionSourceTexture = null;
            if (cubeTexture != null)
            {
                // if the size if different from the cache probe size or if the input texture format is compressed, we need to convert it
                // 1) to a format for which we can generate mip maps
                // 2) to the proper reflection probe cache size
                bool sizeMismatch = cubeTexture.width != m_ProbeSize || cubeTexture.height != m_ProbeSize;
                bool formatMismatch = (GraphicsFormatUtility.GetGraphicsFormat(cubeTexture.format, false) != m_TempRenderTexture.graphicsFormat);
                if (formatMismatch || sizeMismatch)
                {
                    // We comment the following warning as they have no impact on the result but spam the console, it is just that we waste offline time and a bit of quality for nothing.
                    if (sizeMismatch)
                    {
                        // Debug.LogWarningFormat("Baked Reflection Probe {0} does not match HDRP Reflection Probe Cache size of {1}. Consider baking it at the same size for better loading performance.", texture.name, m_ProbeSize);
                    }
                    else if (cubeTexture.graphicsFormat == GraphicsFormat.RGB_BC6H_UFloat || cubeTexture.graphicsFormat == GraphicsFormat.RGB_BC6H_SFloat)
                    {
                        // Debug.LogWarningFormat("Baked Reflection Probe {0} is compressed but the HDRP Reflection Probe Cache is not. Consider removing compression from the input texture for better quality.", texture.name);
                    }
                    ConvertTexture(cmd, cubeTexture, m_TempRenderTexture);
                }
                else
                {
                    for (int f = 0; f < 6; f++)
                    {
                        cmd.CopyTexture(cubeTexture, f, 0, m_TempRenderTexture, f, 0);
                    }
                }

                // Ideally if input is not compressed and has mipmaps, don't do anything here. Problem is, we can't know if mips have been already convolved offline...
                cmd.GenerateMips(m_TempRenderTexture);
                convolutionSourceTexture = m_TempRenderTexture;
            }
            else
            {
                Debug.Assert(renderTexture != null);
                if (renderTexture.dimension != TextureDimension.Cube)
                {
                    Debug.LogError("Realtime reflection probe should always be a Cube RenderTexture.");
                    return null;
                }

                // TODO: Do a different case for downsizing, in this case, instead of doing ConvertTexture just use the relevant mipmaps.
                bool sizeMismatch = renderTexture.width != m_ProbeSize || renderTexture.height != m_ProbeSize;
                bool formatMismatch = (renderTexture.graphicsFormat != m_ProbeFormat);

                if (formatMismatch || sizeMismatch)
                {
                    ConvertTexture(cmd, renderTexture, m_TempRenderTexture);
                    convolutionSourceTexture = m_TempRenderTexture;
                }
                else
                {
                    convolutionSourceTexture = renderTexture;
                }
                // Generate unfiltered mipmaps as a base for convolution
                // TODO: Make sure that we don't first convolve everything on the GPU with the legacy code path executed after rendering the probe.
                cmd.GenerateMips(convolutionSourceTexture);
            }

            {
                CubemapFilter.instance.Filter(cmd, convolutionSourceTexture, m_ConvolutionTargetTexture);
            }

            return m_ConvolutionTargetTexture;
        }

        public int FetchSlice(CommandBuffer cmd, Texture texture)
        {
            bool needUpdate;
            var sliceIndex = m_TextureCache.ReserveSlice(texture, out needUpdate);
            if (sliceIndex != -1)
            {
                m_PrepareRelightTextures[sliceIndex] = texture;

                if (needUpdate)
                {
                    Texture result = ConvolveProbeTexture(cmd, texture);
                    if (result == null)
                        return -1;

                    if (m_PerformBC6HCompression)
                    {
                        cmd.BC6HEncodeFastCubemap(
                            result, m_ProbeSize, m_TextureCache.GetTexCache(),
                            0, int.MaxValue, sliceIndex);
                        m_TextureCache.SetSliceHash(sliceIndex, m_TextureCache.GetTextureHash(texture));
                    }
                    else
                    {
                        m_TextureCache.UpdateSlice(cmd, sliceIndex, result, m_TextureCache.GetTextureHash(texture)); // Be careful to provide the update count from the input texture, not the temporary one used for convolving.
                    }

                    m_HasRelight |= needUpdate;
                }
            }

            return sliceIndex;
        }

        public Texture GetTexCache()
        {
            return m_TextureCache.GetTexCache();
        }

        public void Relight(CommandBuffer cmd, int probeCount)
        {
            if (!m_HasRelight && probeCount > 0)
            {
                m_TextureCache.UpdateSlice(probeCount, out int relightIndex);

                if (relightIndex != -1)
                {
                    Texture texture = m_PrepareRelightTextures[relightIndex];
                    if (texture == null)
                        return;
                    Texture result = ConvolveProbeTexture(cmd, texture);
                    if (result == null)
                        return;

                    if (m_PerformBC6HCompression)
                    {
                        cmd.BC6HEncodeFastCubemap(
                            result, m_ProbeSize, m_TextureCache.GetTexCache(),
                            0, int.MaxValue, relightIndex);
                    }
                    else
                    {
                        m_TextureCache.UpdateSlice(cmd, relightIndex, result, m_TextureCache.GetTextureHash(texture)); // Be careful to provide the update count from the input texture, not the temporary one used for convolving.
                    }

                    m_HasRelight = true;
                }
            }

            for (int i = 0; i < m_PrepareRelightTextures.Length; i++)
            {
                m_PrepareRelightTextures[i] = null;
            }
        }
    }
}
