using Geometry4D;
using UnityEditor;
using UnityEngine;
using System.IO;

public class MeshGen4D
{
    //Enter the vertex coordinates here
    private static MeshData4D Generate4DMeshData()
    {
        Vector4[] sourceVertices = new Vector4[]{
            new Vector4( 1,  1,  1,  1),
            new Vector4( 1,  1,  1, -1),
            new Vector4( 1,  1, -1,  1),
            new Vector4( 1,  1, -1, -1),
            new Vector4( 1, -1,  1,  1),
            new Vector4( 1, -1,  1, -1),
            new Vector4( 1, -1, -1,  1),
            new Vector4( 1, -1, -1, -1),
            new Vector4(-1,  1,  1,  1),
            new Vector4(-1,  1,  1, -1),
            new Vector4(-1,  1, -1,  1),
            new Vector4(-1,  1, -1, -1),
            new Vector4(-1, -1,  1,  1),
            new Vector4(-1, -1,  1, -1),
            new Vector4(-1, -1, -1,  1),
            new Vector4(-1, -1, -1, -1)
        };
        /*
        Vertex example

        Long and thin shapes
        new Vector4( 0.3f,  0.3f,  2,  0.3f),
        new Vector4( 0.3f,  0.3f,  2, -0.3f),
        new Vector4( 0.3f,  0.3f, -2,  0.3f),
        new Vector4( 0.3f,  0.3f, -2, -0.3f),
        new Vector4( 0.3f, -0.3f,  2,  0.3f),
        new Vector4( 0.3f, -0.3f,  2, -0.3f),
        new Vector4( 0.3f, -0.3f, -2,  0.3f),
        new Vector4( 0.3f, -0.3f, -2, -0.3f),
        new Vector4(-0.3f,  0.3f,  2,  0.3f),
        new Vector4(-0.3f,  0.3f,  2, -0.3f),
        new Vector4(-0.3f,  0.3f, -2,  0.3f),
        new Vector4(-0.3f,  0.3f, -2, -0.3f),
        new Vector4(-0.3f, -0.3f,  2,  0.3f),
        new Vector4(-0.3f, -0.3f,  2, -0.3f),
        new Vector4(-0.3f, -0.3f, -2,  0.3f),
        new Vector4(-0.3f, -0.3f, -2, -0.3f)

        Random shapes
        new Vector4(1, -1, 1, 1.5f),
        new Vector4(-1, -1, 1, 1.2f),
        new Vector4(-1, -1, -1, 0.8f),
        new Vector4(1, -1, -1, 0.5f),
        new Vector4(1, 1, 1, 0.2f),
        new Vector4(-1, 1, 1, -0.2f),
        new Vector4(-1, 1, -1, -0.5f),
        new Vector4(1, 1, -1, -0.8f),
        new Vector4(1.2f, -0.8f, 0.5f, -1.2f),
        new Vector4(-0.8f, -1.2f, -0.5f, -1.5f),
        new Vector4(-1.5f, -0.5f, -1.2f, 1.0f),
        new Vector4(0.5f, -1.5f, 0.8f, 0.0f),
        new Vector4(0.8f, 1.5f, -1.0f, -1.0f),
        new Vector4(-1.2f, 0.5f, 1.5f, 0.7f),
        new Vector4(-0.5f, 1.2f, -1.5f, -0.3f),
        new Vector4(1.5f, 0.8f, 0.2f, 1.3f)
        */

        if (sourceVertices == null || sourceVertices.Length == 0) return null;

        Debug.Log("Generating 4D Convex Hull from hardcoded vertices...");

        BuildConvexMesh4D convexDefine4D = new BuildConvexMesh4D();
        FacetInfo4D[] faces = convexDefine4D.GenerateConvex(sourceVertices);
        
        MeshData4D data = ScriptableObject.CreateInstance<MeshData4D>();
        
        data.Vertices = sourceVertices;
        data.SurfaceFaces = faces;

        return data;
    }

    [MenuItem("Tools/4D Mesh/Save Generated Mesh as ScriptableObject")]
    private static void SaveAsScriptableObject()
    {
        MeshData4D data = Generate4DMeshData();
        if (data == null) return;
        
        string path = EditorUtility.SaveFilePanelInProject(
            "Save 4D Convex Mesh Data as Asset", 
            "NewConvexData4D", 
            "asset", 
            ""
        );

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"4D Convex data saved to: {path}");
        }
        else
        {
            Object.DestroyImmediate(data);
        }
    }

    [MenuItem("Tools/4D Mesh/Save Generated Mesh as JSON in Assets")]
    private static void SaveAsJSONInAssets()
    {
        MeshData4D data = Generate4DMeshData();
        if (data == null) return;
        string json = JsonUtility.ToJson(data, true);

        Object.DestroyImmediate(data);

        string path = EditorUtility.SaveFilePanel(
            "Save 4D Mesh Data as JSON in Assets",
            Application.dataPath, 
            "NewConvexData4D",
            "json"
        );

        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            Debug.Log($"4D JSON data saved to: {path}");
            AssetDatabase.Refresh();
        }
    }
}
