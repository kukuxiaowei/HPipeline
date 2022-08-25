using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace HPipeline
{
    public partial class RenderPipeline
    {
        struct LightData
        {
            public Vector4 Position;
            public Vector4 Color;
        }

        const int MaxLightCount = 256;

        List<LightData> _lightDataArray;
        ComputeBuffer _lightDataBuffer;

        void LightDataInit()
        {
            _lightDataArray = new List<LightData>();
            _lightDataBuffer = new ComputeBuffer(MaxLightCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(LightData)));
        }

        void LightDataSetup(CommandBuffer cmd, CullingResults cullingResults, out ComputeBuffer lightDataBuffer)
        {
            _lightDataArray.Clear();
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
                    var lightData = new LightData()
                    {
                        Position = visibleLight.localToWorldMatrix.GetColumn(3),
                        Color = visibleLight.finalColor
                    };
                    lightData.Position.w = visibleLight.range;
                    _lightDataArray.Add(lightData);
                }
            }
            _lightDataBuffer.SetData(_lightDataArray);
            lightDataBuffer = _lightDataBuffer;

            cmd.SetGlobalInt(ShaderIDs._LightCount, _lightDataArray.Count);
        }

        void LightDataCleanup()
        {
            if (_lightDataBuffer != null)
                _lightDataBuffer.Release();
        }
    }
}