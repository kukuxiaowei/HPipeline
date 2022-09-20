using UnityEngine;
using UnityEngine.Rendering;

namespace HPipeline
{
    public partial class RenderPipeline
    {
        struct LightData
        {
            public Vector4 Position;
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
            _lightCount = Mathf.Min(cullingResults.visibleLights.Length, maxLightCount);
            for (int i = 0; i < _lightCount; i++)
            {
                var visibleLight = cullingResults.visibleLights[i];
                if(visibleLight.lightType == LightType.Directional)
                {
                    cmd.SetGlobalVector(ShaderIDs._MainLightPosition, -visibleLight.localToWorldMatrix.GetColumn(2));
                    cmd.SetGlobalVector(ShaderIDs._MainLightColor, visibleLight.finalColor);
                }
                else if(visibleLight.lightType == LightType.Point)
                {
                    _lightDataArray[i] = new LightData()
                    {
                        Position = visibleLight.localToWorldMatrix.GetColumn(3),
                        Color = visibleLight.finalColor
                    };
                    _lightDataArray[i].Position.w = visibleLight.range;
                }
            }
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