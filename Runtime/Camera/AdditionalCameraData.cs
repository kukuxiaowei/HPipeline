using UnityEngine;

namespace HPipeline
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class AdditionalCameraData : MonoBehaviour
    {
        public GBufferProbe probe;
    }
}
