using System.IO;
using UnityEditor;
using UnityEngine;

namespace HPipeline
{
    static class AssetFactory
    {
        class DoCreateNewAsset : UnityEditor.ProjectWindowCallback.EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var newAsset = CreateInstance<RenderPipelineAsset>();
                newAsset.name = Path.GetFileName(pathName);

                AssetDatabase.CreateAsset(newAsset, pathName);
                ProjectWindowUtil.ShowCreatedAsset(newAsset);
            }
        }

        [MenuItem("Assets/Create/Rendering/HPipeline Asset", priority = 201)]
        static void CreateRenderPipeline()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateNewAsset>(),
                "New HPipeline Asset.asset", null, null);
        }
    }
}