#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class BooleanResultImporter
{
    //3D Mesh Importer
    [MenuItem("Assets/Convert to 3D Mesh (ScriptableObject)", false, 10)]
    private static void ConvertJsonTo3DMesh()
    {
        ProcessJsonImport<MeshData3D>();
    }

    [MenuItem("Assets/Convert to 3D Mesh (ScriptableObject)", true)]
    private static bool ValidateConvertJsonTo3DMesh()
    {
        //Verify that the selected JSON is 3D data
        return IsJsonContent3D();
    }

    //4D Mesh Importer
    [MenuItem("Assets/Convert to 4D Mesh (ScriptableObject)", false, 11)]
    private static void ConvertJsonTo4DMesh()
    {
        ProcessJsonImport<MeshData4D>();
    }

    [MenuItem("Assets/Convert to 4D Mesh (ScriptableObject)", true)]
    private static bool ValidateConvertJsonTo4DMesh()
    {
        //Verify that the selected JSON is 4D data
        return IsJsonContent4D();
    }

    private static void ProcessJsonImport<T>() where T : ScriptableObject
    {
        var jsonAsset = Selection.activeObject as TextAsset;
        if (jsonAsset == null) return;

        string sourcePath = AssetDatabase.GetAssetPath(jsonAsset);

        try
        {
            T newAsset = ScriptableObject.CreateInstance<T>();
            JsonUtility.FromJsonOverwrite(jsonAsset.text, newAsset);

            string savePath = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(sourcePath, ".asset"));

            AssetDatabase.CreateAsset(newAsset, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Successfully imported and saved asset to: {savePath}");

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newAsset;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to import JSON file: {e.Message}");
            EditorUtility.DisplayDialog("Import Error", e.Message, "OK");
        }
    }

    //Validation logic to determine the contents of JSON
    private static bool IsJsonContent3D()
    {
        string jsonText = GetSelectedJsonText();
        if (string.IsNullOrEmpty(jsonText)) return false;

        // Verify that the w key does not exist
        int verticesIndex = jsonText.IndexOf("\"Vertices\"");
        if (verticesIndex == -1) return false;
        
        int firstBraceIndex = jsonText.IndexOf('{', verticesIndex);
        if (firstBraceIndex == -1) return false;
        
        int firstWIndex = jsonText.IndexOf("\"w\"", firstBraceIndex);
        int firstClosingBraceIndex = jsonText.IndexOf('}', firstBraceIndex);
        
        // If w is not found or is outside the first element of vertices, assume 3D
        return firstWIndex == -1 || firstWIndex > firstClosingBraceIndex;
    }

    private static bool IsJsonContent4D()
    {
        string jsonText = GetSelectedJsonText();
        if (string.IsNullOrEmpty(jsonText)) return false;
        
        // Verify that the w key exists
        int verticesIndex = jsonText.IndexOf("\"Vertices\"");
        if (verticesIndex == -1) return false;
        
        int firstBraceIndex = jsonText.IndexOf('{', verticesIndex);
        if (firstBraceIndex == -1) return false;
        
        int firstWIndex = jsonText.IndexOf("\"w\"", firstBraceIndex);
        int firstClosingBraceIndex = jsonText.IndexOf('}', firstBraceIndex);

        // If w is found and is inside the first element of vertices, it is considered 4D.
        return firstWIndex != -1 && firstWIndex < firstClosingBraceIndex;
    }
    
    private static string GetSelectedJsonText()
    {
        var selectedObject = Selection.activeObject;
        if (selectedObject is TextAsset jsonAsset)
        {
            string path = AssetDatabase.GetAssetPath(jsonAsset);
            if (Path.GetExtension(path).ToLower() == ".json")
            {
                return jsonAsset.text;
            }
        }
        return null;
    }
}
#endif
