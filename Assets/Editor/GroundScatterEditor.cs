using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VoidDay.View;

namespace VoidDay.EditorTools
{
    /// Inspector buttons for GroundScatter. The bake writes one combined mesh asset and points the
    /// scatter's MeshFilter at it, so the grass ships as a single draw call and the scene file grows
    /// by one object rather than by several hundred.
    [CustomEditor(typeof(GroundScatter))]
    public sealed class GroundScatterEditor : Editor
    {
        const string MeshPath = "Assets/Art/Models/Environment/GrassScatter.asset";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            var scatter = (GroundScatter)target;
            if (GUILayout.Button("Bake Scatter")) Bake(scatter);
            if (GUILayout.Button("Clear Scatter")) Clear(scatter);
        }

        static void Bake(GroundScatter scatter)
        {
            var built = scatter.BuildCombinedMesh();
            if (built == null) throw new System.InvalidOperationException($"{scatter.name}: bake placed no tufts");

            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(built, MeshPath);
                asset = built;
            }
            else
            {
                // Overwrite in place so every existing reference to the asset survives a re-bake.
                EditorUtility.CopySerialized(built, asset);
                Object.DestroyImmediate(built);
            }

            scatter.BakeTarget.sharedMesh = asset;
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(scatter.BakeTarget);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scatter.gameObject.scene);
            Debug.Log($"Baked grass scatter: {asset.vertexCount} verts, {asset.triangles.Length / 3} tris → {MeshPath}");
        }

        static void Clear(GroundScatter scatter)
        {
            scatter.BakeTarget.sharedMesh = null;
            EditorUtility.SetDirty(scatter.BakeTarget);
            EditorSceneManager.MarkSceneDirty(scatter.gameObject.scene);
        }
    }
}
