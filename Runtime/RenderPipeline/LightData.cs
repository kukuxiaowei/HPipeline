using UnityEngine;
using UnityEngine.Rendering;

namespace HPipeline
{
    [GenerateHLSL]
    enum LightCategory
    {
        Punctual,
        Env,
        Count
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    struct DirectionalLightData
    {
        public Vector4 positionWS;
        public Vector4 color;
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    struct PunctualLightData
    {
        public Vector3 positionWS;
        public float range;

        public Vector3 color;
        public float spotAngleScale;

        public Vector3 forward;
        public float spotAngleOffset;
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    struct EnvLightData
    {
        public int envIndex;
        public Vector3 positionWS;
        public float range;
        public float blendDistance;
        public Vector2 padding;
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    struct LightBound
    {
        public Vector3 position;
        public float range;

        public Vector3 forward;
        public float spotCosAngle;

        public float spotSinAngle;
        public LightCategory category;
        public uint categoryOffset;
        public uint padding;

        public LightBound(PunctualLightData punctualLight, uint categoryOffset = 0)
        {
            this.position = punctualLight.positionWS;
            this.range = punctualLight.range;
            this.forward = punctualLight.forward;
            this.spotCosAngle = 1.0f;
            this.spotSinAngle = 0.0f;
            this.category = LightCategory.Punctual;
            this.categoryOffset = categoryOffset;
            this.padding = 0;
        }

        public LightBound(EnvLightData envLight, uint categoryOffset)
        {
            this.position = envLight.positionWS;
            this.range = envLight.range;
            this.forward = Vector3.zero;
            this.spotCosAngle = 1.0f;
            this.spotSinAngle = 0.0f;
            this.category = LightCategory.Env;
            this.categoryOffset = categoryOffset;
            this.padding = 0;
        }
    }

    internal class LightData
    {
        public GraphicsBuffer directionalLightData { get; private set; }
        public GraphicsBuffer punctualLightData { get; private set; }
        public GraphicsBuffer envLightData { get; private set; }

        public int directionalLightCount;
        public int punctualLightCount;
        public int envLightCount;

        public void Initialize(int directionalCount, int punctualCount, int envLightCount)
        {
            directionalLightData = new GraphicsBuffer(GraphicsBuffer.Target.Structured, directionalCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(DirectionalLightData)));
            punctualLightData = new GraphicsBuffer(GraphicsBuffer.Target.Structured, punctualCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(PunctualLightData)));
            envLightData = new GraphicsBuffer(GraphicsBuffer.Target.Structured, envLightCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(EnvLightData)));
        }

        public void Cleanup()
        {
            CoreUtils.SafeRelease(directionalLightData);
            CoreUtils.SafeRelease(punctualLightData);
            CoreUtils.SafeRelease(envLightData);
        }

        public void SetDirectionalLightData(DynamicArray<DirectionalLightData> directionalLights)
        {
            this.directionalLightCount = directionalLights.size;
            this.directionalLightData.SetData(directionalLights, 0, 0, this.directionalLightCount);
        }

        public void SetPunctualLightData(DynamicArray<PunctualLightData> punctualLights)
        {
            this.punctualLightCount = punctualLights.size;
            this.punctualLightData.SetData(punctualLights, 0, 0, this.punctualLightCount);
        }

        public void SetEnvLightData(DynamicArray<EnvLightData> envLights)
        {
            this.envLightCount = envLights.size;
            this.envLightData.SetData(envLights, 0, 0, this.envLightCount);
        }
    }
}