using UnityEditor;
using UnityEngine;

namespace HPipeline
{
    [CustomEditor(typeof(GBufferProbe))]
    public class GBufferProbeInspector : Editor
    {
        private GBufferProbe probe => target as GBufferProbe;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Bake GBuffer"))
            {
                probe.BakeProbe();
            }
        }
    }
}