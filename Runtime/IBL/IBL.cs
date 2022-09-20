using UnityEngine;
using Object = UnityEngine.Object;

namespace HPipeline
{
    public struct ProbeData
    {
        public int Id;
        public Vector3 Center;
        public Vector3 Size;
        public float BlendDistance;
    }
    
    public class IBL
    {
        private const int resolution = 512;
        private const int maxProbeCount = 16;
        
        private static IBL _instance;

        public static IBL instance
        {
            get
            {
                if (_instance == null)
                    _instance = new IBL();

                return _instance;
            }
        }

        public int ProbesCount = 0;
        private readonly ProbeData[] _probesDataArray = new ProbeData[maxProbeCount];
        public ComputeBuffer ProbesDataBuffer;
        public readonly Texture2DArray ProbesTexture = new Texture2DArray(resolution, resolution, maxProbeCount, TextureFormat.RGBAHalf, true, false);
        
        public void ProbesDataInit()
        {
            ProbesDataBuffer = new ComputeBuffer(maxProbeCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ProbeData)));
        }

        private bool _needRecombine = true;
        public void ProbesDataSetup()
        {
            if (Application.isPlaying && _needRecombine)
            {
                _needRecombine = false;
                
                CombineProbes();
            }

            if (false == Application.isPlaying)
            {
                _needRecombine = true;
            }
        }

        public void CombineProbes()
        {
            ReflectionProbe[] probes = Object.FindObjectsOfType<ReflectionProbe>();
            ProbesCount = Mathf.Min(probes.Length, maxProbeCount);

            for (int i = 0; i < ProbesCount; i++)
            {
                var probe = probes[i];
                for (int m = 0; m < probe.texture.mipmapCount; m++)
                {
                    Graphics.CopyTexture(probe.texture, 0, m, ProbesTexture, i, m);
                }

                _probesDataArray[i] = new ProbeData()
                {
                    Id = i,
                    Center = probe.center,
                    Size = probe.size,
                    BlendDistance = probe.blendDistance,
                };
            }
            
            ProbesDataBuffer.SetData(_probesDataArray, 0, 0, ProbesCount);
        }
        
        public void ProbesDataCleanup()
        {
            if (ProbesDataBuffer != null)
                ProbesDataBuffer.Release();
        }
    }
}