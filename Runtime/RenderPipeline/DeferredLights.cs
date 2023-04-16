using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace HPipeline
{
    internal class DeferredLights
    {
        const int k_ReflectionProbeResolution = 128; // 2^7

        const int k_MaxDirectionalLights = 16;
        const int k_MaxPunctualLights = 512;
        const int k_MaxEnvLights = 16;

        ReflectionProbeCache m_ReflectionProbeCache;

        LightData m_LightData = new LightData();

        DynamicArray<DirectionalLightData> m_DirectionalLights = new DynamicArray<DirectionalLightData>();
        DynamicArray<PunctualLightData> m_PunctualLights = new DynamicArray<PunctualLightData>();
        DynamicArray<EnvLightData> m_EnvLights = new DynamicArray<EnvLightData>();

        DynamicArray<LightBound> m_LightBounds = new DynamicArray<LightBound>();
        public DynamicArray<LightBound> LightBounds => (m_LightBounds);

        static public int MaxLights => (k_MaxPunctualLights + k_MaxEnvLights);
        static public int MaxReflectionProbes => (k_MaxEnvLights);

        internal DeferredLights()
        {
            m_ReflectionProbeCache = new ReflectionProbeCache(k_MaxEnvLights, k_ReflectionProbeResolution, GraphicsFormat.R16G16B16A16_SFloat, true);

            m_LightData.Initialize(k_MaxDirectionalLights, k_MaxPunctualLights, k_MaxEnvLights);
        }

        internal void Dispose()
        {
            m_ReflectionProbeCache.Release();

            m_LightData.Cleanup();
        }

        internal void SetupLights(ref RenderingData renderingData)
        {
            m_ReflectionProbeCache.NewFrame();

            m_LightBounds.Clear();

            var cmd = renderingData.commandBuffer;

            PreprocessVisibleLights(renderingData.cullResults.visibleLights);
            PreprocessVisibleProbes(cmd, renderingData.cullResults.visibleReflectionProbes);

            m_LightData.SetDirectionalLightData(m_DirectionalLights);
            m_LightData.SetPunctualLightData(m_PunctualLights);
            m_LightData.SetEnvLightData(m_EnvLights);

            SetupShaderLightConstants(cmd);
        }

        void PreprocessVisibleLights(in NativeArray<VisibleLight> visibleLights)
        {
            if (visibleLights.Length == 0)
            {
                return;
            }

            int totalDirectionalLightCount = 0;
            int totalPunctualLightCount = 0;
            for (int visLightIndex = 0; visLightIndex < visibleLights.Length; visLightIndex++)
            {
                VisibleLight vl = visibleLights[visLightIndex];

                if (IsPunctualLight(vl))
                    ++totalPunctualLightCount;
                else if (vl.lightType == LightType.Directional)
                    ++totalDirectionalLightCount;
            }
            totalDirectionalLightCount = Mathf.Min(totalDirectionalLightCount, k_MaxDirectionalLights);
            totalPunctualLightCount = Mathf.Min(totalPunctualLightCount, k_MaxPunctualLights);

            m_DirectionalLights.Resize(totalDirectionalLightCount);
            m_PunctualLights.Resize(totalPunctualLightCount);

            int directionalLightCount = 0;
            int punctualLightCount = 0;
            for (int visLightIndex = 0; visLightIndex < visibleLights.Length; visLightIndex++)
            {
                VisibleLight vl = visibleLights[visLightIndex];

                if (IsPunctualLight(vl) && punctualLightCount < totalPunctualLightCount)
                {
                    ref PunctualLightData punctualLight = ref m_PunctualLights[punctualLightCount];
                    punctualLightCount++;

                    punctualLight.positionWS = vl.localToWorldMatrix.GetColumn(3);
                    punctualLight.forward = vl.localToWorldMatrix.GetColumn(2);
                    punctualLight.range = vl.range;
                    punctualLight.color = new Vector3(vl.finalColor.r, vl.finalColor.g, vl.finalColor.b);

                    LightBound lightBound = new LightBound(punctualLight);

                    if (vl.lightType == LightType.Spot)
                    {
                        float cosOuterAngle = Mathf.Cos(Mathf.Deg2Rad * vl.spotAngle * 0.5f);
                        float smoothAngleRange = Mathf.Clamp(1.0f - cosOuterAngle, 0.0001f, 1.0f);
                        float invAngleRange = 1.0f / smoothAngleRange;
                        float add = -cosOuterAngle * invAngleRange;

                        punctualLight.spotAngleScale = invAngleRange;
                        punctualLight.spotAngleOffset = add;

                        lightBound.spotCosAngle = cosOuterAngle;
                        lightBound.spotSinAngle = Mathf.Sin(Mathf.Deg2Rad * vl.spotAngle * 0.5f);
                    }
                    else
                    {
                        punctualLight.spotAngleScale = 0.0f;
                        punctualLight.spotAngleOffset = 1.0f;
                    }

                    m_LightBounds.Add(lightBound);
                }
                else if (vl.lightType == LightType.Directional && directionalLightCount < totalDirectionalLightCount)
                {
                    ref DirectionalLightData directionalLight = ref m_DirectionalLights[directionalLightCount];
                    directionalLightCount++;

                    directionalLight.positionWS = -vl.localToWorldMatrix.GetColumn(2);
                    directionalLight.color = vl.finalColor;
                }
            }
        }

        void PreprocessVisibleProbes(CommandBuffer cmd, in NativeArray<VisibleReflectionProbe> visibleReflectionProbes)
        {
            uint categoryOffset = (uint)m_PunctualLights.size;
            m_EnvLights.Resize(visibleReflectionProbes.Length);

            int envLightCount = 0;
            for (int probeIndex = 0; probeIndex < visibleReflectionProbes.Length; probeIndex++)
            {
                if (envLightCount >= k_MaxEnvLights)
                    continue;

                VisibleReflectionProbe probe = visibleReflectionProbes[probeIndex];

                if (probe.reflectionProbe == null || !probe.reflectionProbe.isActiveAndEnabled
                    || probe.texture == null)
                    continue;

                ref EnvLightData envlight = ref m_EnvLights[envLightCount];
                envLightCount++;

                envlight.positionWS = probe.reflectionProbe.bounds.center;
                envlight.range = probe.reflectionProbe.bounds.extents.magnitude;
                envlight.blendDistance = Mathf.Min(probe.reflectionProbe.blendDistance, envlight.range);

                envlight.envIndex = m_ReflectionProbeCache.FetchSlice(cmd, probe.texture);

                m_LightBounds.Add(new LightBound(envlight, categoryOffset));
            }
        }

        void SetupShaderLightConstants(CommandBuffer cmd)
        {
            cmd.SetGlobalTexture(ShaderPropertyId.envCubemapArray, m_ReflectionProbeCache.GetTexCache());

            cmd.SetGlobalBuffer(ShaderPropertyId.directionalLightDatas, m_LightData.directionalLightData);
            cmd.SetGlobalBuffer(ShaderPropertyId.punctualLightDatas, m_LightData.punctualLightData);
            cmd.SetGlobalBuffer(ShaderPropertyId.envLightDatas, m_LightData.envLightData);

            cmd.SetGlobalInt(ShaderPropertyId.directionalLightCount, m_LightData.directionalLightCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsPunctualLight(in VisibleLight visibleLight)
        {
            return (visibleLight.lightType == LightType.Point || visibleLight.lightType == LightType.Spot);
        }
    }
}