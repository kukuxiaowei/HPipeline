using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HPipeline
{
    public enum FrameResourceType
    {
        /// <summary>
        /// The backbuffer color used to render directly to screen. All passes can write to it depending on frame setup.
        /// </summary>
        BackBufferColor,

        /// <summary>
        /// The backbuffer depth used to render directly to screen. All passes can write to it depending on frame setup.
        /// </summary>
        BackBufferDepth,

        // intermediate camera targets

        /// <summary>
        /// Main offscreen camera color target. All passes can write to it depending on frame setup.
        /// Can hold multiple samples if MSAA is enabled.
        /// </summary>
        CameraColor,
        /// <summary>
        /// Main offscreen camera depth target. All passes can write to it depending on frame setup.
        /// Can hold multiple samples if MSAA is enabled.
        /// </summary>
        CameraDepth,

        // shadows

        /// <summary>
        /// Main shadow map.
        /// </summary>
        MainShadowsTexture,
        /// <summary>
        /// Additional shadow map.
        /// </summary>
        AdditionalShadowsTexture,

        // gbuffer targets

        /// <summary>
        /// GBuffer0. Written to by the GBuffer pass.
        /// </summary>
        GBuffer0,
        /// <summary>
        /// GBuffer1. Written to by the GBuffer pass.
        /// </summary>
        GBuffer1,
        /// <summary>
        /// GBuffer2. Written to by the GBuffer pass.
        /// </summary>
        GBuffer2,
        /// <summary>
        /// GBuffer3. Written to by the GBuffer pass.
        /// </summary>
        GBuffer3,
        /// <summary>
        /// GBuffer4. Written to by the GBuffer pass.
        /// </summary>
        GBuffer4,

        // motion vector

        /// <summary>
        /// Motion Vector Color. Written to by the Motion Vector passes.
        /// </summary>
        MotionVectorColor,
        /// <summary>
        /// Motion Vector Depth. Written to by the Motion Vector passes.
        /// </summary>
        MotionVectorDepth,

        // postFx

        /// <summary>
        /// Internal Color LUT. Written to by the InternalLUT pass.
        /// </summary>
        InternalColorLut,

        // decals

        /// <summary>
        /// DBuffer0. Written to by the Decals pass.
        /// </summary>
        DBuffer0,
        /// <summary>
        /// DBuffer1. Written to by the Decals pass.
        /// </summary>
        DBuffer1,
        /// <summary>
        /// DBuffer2. Written to by the Decals pass.
        /// </summary>
        DBuffer2,

        /// <summary>
        /// DBufferDepth. Written to by the Decals pass.
        /// </summary>
        DBufferDepth
    }

    public class FrameResources
    {
        Dictionary<Hash128, TextureHandle> m_TextureHandles = new Dictionary<Hash128, TextureHandle>();

        static uint s_TypeCount;

        static class TypeId<T>
        {
            public static uint value = s_TypeCount++;
        }

        internal void InitFrame()
        {
            m_TextureHandles.Clear();
        }

        Hash128 GetKey<T>(T id) where T : unmanaged, Enum
        {
            Hash128 hash = new Hash128();
            var typeId = TypeId<T>.value;
            hash.Append(typeId);
            hash.Append(ref id);

            return hash;
        }

        /// <summary>
        /// Get the TextureHandle for a specific identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public TextureHandle GetTexture<T>(T id) where T : unmanaged, Enum
        {
            if (m_TextureHandles.TryGetValue(GetKey(id), out TextureHandle handle))
                return handle;

            return TextureHandle.nullHandle;
        }

        /// <summary>
        /// Add or replace the TextureHandle for a specific identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="handle"></param>
        /// <typeparam name="T"></typeparam>
        public void SetTexture<T>(T id, TextureHandle handle) where T : unmanaged, Enum
        {
            m_TextureHandles[GetKey(id)] = handle;
        }

        public void SetFrameResourcesGBufferArray(TextureHandle[] gbuffer)
        {
            for (int i = 0; i < gbuffer.Length; ++i)
                SetTexture((FrameResourceType.GBuffer0 + i), gbuffer[i]);
        }

        public TextureHandle[] GetFrameResourcesGBufferArray(int gbufferSliceCount)
        {
            TextureHandle[] gbuffer = new TextureHandle[gbufferSliceCount];

            for (int i = 0; i < gbuffer.Length; ++i)
                gbuffer[i] = GetTexture((FrameResourceType.GBuffer0 + i));

            return gbuffer;
        }
    }
}

