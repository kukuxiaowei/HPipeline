using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HPipeline
{
    public class IntegratedBRDF
    {
        private const int size = 64;
        private static readonly string Path = Application.dataPath + "/HPipeline/Runtime/Resources/IntegratedBRDF.png";

        [MenuItem("Assets/Create/Rendering/PreCompute/IntegratedBRDF")]
        public static void CreateIntegratedBRDF()
        {
            Texture2D preComputeTex = new Texture2D(size, size);

            RenderTexture preComputeRT = new RenderTexture(size, size, 0)
            {
                useMipMap = false,
                autoGenerateMips = false
            };

            var material = new Material(Shader.Find("Hidden/IntegrateBRDF"));

            CommandBuffer cmd = new CommandBuffer();
            Graphics.SetRenderTarget(preComputeRT);
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        
            //copy from RT to texture
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = preComputeRT;
            preComputeTex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            preComputeTex.Apply();
            RenderTexture.active = previous;
        
            byte[] bytes = preComputeTex.EncodeToPNG();
            File.WriteAllBytes(Path, bytes);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        
            //Modification Texture 
            TextureImporter importer = AssetImporter.GetAtPath(Path) as TextureImporter;
            if (importer != null)
            {
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }
    }
}