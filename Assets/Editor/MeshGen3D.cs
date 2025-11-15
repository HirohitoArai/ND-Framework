using Geometry3D;
using System.IO;
using UnityEditor;
using UnityEngine;

public class MeshGen3D
{
    //Enter the vertex coordinates here
    private static MeshData3D Generate3DMeshData()
    {
        Vector3[] sourceVertices = new Vector3[]{
            new Vector3(+1, +1, +1),
            new Vector3(+1, +1, -1),
            new Vector3(+1, -1, +1),
            new Vector3(+1, -1, -1),
            new Vector3(-1, +1, +1),
            new Vector3(-1, +1, -1),
            new Vector3(-1, -1, +1),
            new Vector3(-1, -1, -1)
        };
        /*
        Vertex example

        Long and thin shapes
        new Vector3( 0.3f,  0.3f,  2f),
        new Vector3( 0.3f,  0.3f, -2f),
        new Vector3( 0.3f, -0.3f,  2f),
        new Vector3( 0.3f, -0.3f, -2f),
        new Vector3(-0.3f,  0.3f,  2f),
        new Vector3(-0.3f,  0.3f, -2f),
        new Vector3(-0.3f, -0.3f,  2f),
        new Vector3(-0.3f, -0.3f, -2f)

        Random shapes
        new Vector3(1, -1, 1),
        new Vector3(-1, -1, 1),
        new Vector3(-1, -1, -1),
        new Vector3(1, -1, -1),
        new Vector3(1, 1, 1),
        new Vector3(-1, 1, 1),
        new Vector3(-1, 1, -1),
        new Vector3(1, 1, -1),
        new Vector3(1.2f, -0.8f, 0.5f),
        new Vector3(-0.8f, -1.2f, -0.5f),
        new Vector3(-1.5f, -0.5f, -1.2f),
        new Vector3(0.5f, -1.5f, 0.8f),
        new Vector3(0.8f, 1.5f, -1.0f),
        new Vector3(-1.2f, 0.5f, 1.5f),
        new Vector3(-0.5f, 1.2f, -1.5f),
        new Vector3(1.5f, 0.8f, 0.2f)
        */

        if (sourceVertices == null || sourceVertices.Length == 0) return null;

        Debug.Log("Generating 4D Convex Hull from hardcoded vertices...");

        BuildConvexMesh3D convexDefine4D = new BuildConvexMesh3D();
        FacetInfo3D[] faces = convexDefine4D.GenerateConvex(sourceVertices);
        
        MeshData3D data = ScriptableObject.CreateInstance<MeshData3D>();
        
        data.Vertices = sourceVertices;
        data.SurfaceFaces = faces;

        return data;
    }

    [MenuItem("Tools/3D Mesh/Save Generated Mesh as ScriptableObject")]
    private static void SaveAsScriptableObject()
    {
        MeshData3D data = Generate3DMeshData();
        if (data == null) return;
        
        string path = EditorUtility.SaveFilePanelInProject(
            "Save 3D Convex Mesh Data as Asset", 
            "NewConvexData3D", 
            "asset", 
            ""
        );

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"3D Convex data saved to: {path}");
        }
        else
        {
            Object.DestroyImmediate(data);
        }
    }

    [MenuItem("Tools/3D Mesh/Save Generated Mesh as JSON in Assets")]
    private static void SaveAsJSONInAssets()
    {
        MeshData3D data = Generate3DMeshData();
        if (data == null) return;
        string json = JsonUtility.ToJson(data, true);

        Object.DestroyImmediate(data);

        string path = EditorUtility.SaveFilePanel(
            "Save 3D Mesh Data as JSON in Assets",
            Application.dataPath,
            "NewConvexData3D",
            "json"
        );

        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            Debug.Log($"3D JSON data saved to: {path}");
            AssetDatabase.Refresh();
        }
    }
}
