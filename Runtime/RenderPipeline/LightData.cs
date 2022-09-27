using UnityEngine;
using UnityEngine.Rendering;

namespace HPipeline
{
    public partial class RenderPipeline
    {
        struct LightData
        {
            public Vector4 Position;
            public Vector4 SpotDirection;
            public Vector4 Color;
        }

        const int maxLightCount = 256;

        int _lightCount = 0;
        readonly LightData[] _lightDataArray = new LightData[maxLightCount];
        ComputeBuffer _lightDataBuffer;

        void LightDataInit()
        {
            _lightDataBuffer = new ComputeBuffer(maxLightCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(LightData)));
        }

        void LightDataSetup(CommandBuffer cmd, CullingResults cullingResults, out ComputeBuffer lightDataBuffer)
        {
            _lightCount = 0;
            for (int i = 0; i < cullingResults.visibleLights.Length; i++)
            {
                var visibleLight = cullingResults.visibleLights[i];
                if(visibleLight.lightType == LightType.Directional)
                {
                    cmd.SetGlobalVector(ShaderIDs._MainLightPosition, -visibleLight.localToWorldMatrix.GetColumn(2));
                    cmd.SetGlobalVector(ShaderIDs._MainLightColor, visibleLight.finalColor);
                }
                else if(visibleLight.lightType == LightType.Point)
                {
                    _lightDataArray[_lightCount] = new LightData()
                    {
                        Position = visibleLight.localToWorldMatrix.GetColumn(3),
                        SpotDirection = Vector4.zero,
                        Color = visibleLight.finalColor
                    };
                    _lightDataArray[_lightCount].Position.w = visibleLight.range;
                    
                    ++_lightCount;
                }
                else if(visibleLight.lightType == LightType.Spot)
                {
                    _lightDataArray[_lightCount] = new LightData()
                    {
                        Position = visibleLight.localToWorldMatrix.GetColumn(3),
                        SpotDirection = visibleLight.localToWorldMatrix.GetColumn(2),
                        Color = visibleLight.finalColor
                    };
                    _lightDataArray[_lightCount].Position.w = visibleLight.range;

                    float cosAngle = Mathf.Cos(Mathf.Deg2Rad * visibleLight.spotAngle * 0.5f);
                    float cosInnerAngle = Mathf.Cos((2.0f * Mathf.Atan(Mathf.Tan(visibleLight.spotAngle * 0.5f * Mathf.Deg2Rad) * (64.0f - 18.0f) / 64.0f)) * 0.5f);//URP
                    _lightDataArray[_lightCount].SpotDirection.w = cosAngle;
                    _lightDataArray[_lightCount].Color.w = cosInnerAngle;
                    
                    ++_lightCount;
                }
            }
            _lightCount = Mathf.Min(_lightCount, maxLightCount);
            _lightDataBuffer.SetData(_lightDataArray, 0, 0, _lightCount);
            lightDataBuffer = _lightDataBuffer;

            cmd.SetGlobalBuffer(ShaderIDs._LightData, _lightDataBuffer);
            cmd.SetGlobalFloat(ShaderIDs._LightCount, _lightCount);
        }

        void LightDataCleanup()
        {
            if (_lightDataBuffer != null)
                _lightDataBuffer.Release();
        }
    }
}