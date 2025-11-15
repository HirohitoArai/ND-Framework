using System.Text;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class Plex2Json
{
    private const string ConvertToJsonMenuPath = "Assets/Convert plex to JSON";
    [MenuItem(ConvertToJsonMenuPath, false, 14)]
    private static void ConvertPlexToJson()
    {
        //Select the .plex file you want to convert
        string inputPath = EditorUtility.OpenFilePanel("Select .plex file to convert", "", "plex");
        if (string.IsNullOrEmpty(inputPath)) return;

        try
        {
            //Read binary files with PlexParser and generate PlexData
            Debug.Log($"Reading .plex file from: {inputPath}");
            PlexData plexData = PlexParser.Read(inputPath);
            if (plexData == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to parse the .plex file.", "OK");
                return;
            }

            //Convert PlexData to JSON string
            Debug.Log("Converting PlexData to JSON string...");
            string jsonString = ToJson(plexData);

            //Prompt the user for the save path
            string initialFileName = $"{Path.GetFileNameWithoutExtension(inputPath)}.json";
            string outputPath = EditorUtility.SaveFilePanel("Save JSON file", "", initialFileName, "json");
            if (string.IsNullOrEmpty(outputPath)) return;

            //Write a JSON string to a file
            File.WriteAllText(outputPath, jsonString);
            Debug.Log($"<color=green>Successfully converted to JSON at: {outputPath}</color>");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Conversion failed: {e.Message}\n{e.StackTrace}");
        }
    }
    public static string ToJson(PlexData data)
    {
        var sb = new StringBuilder();

        sb.AppendLine("{");

        // --- dimension ---
        sb.AppendLine($"  \"dimension\": {data.Dimension},");

        // --- vertices ---
        sb.AppendLine("  \"vertices\": [");
        for (int i = 0; i < data.Vertices.Count; i++)
        {
            var vertex = data.Vertices[i];
            sb.Append("    [");
            for (int j = 0; j < data.Dimension; j++)
            {
                sb.Append(vertex.Coordinates[j].ToString(CultureInfo.InvariantCulture));
                if (j < data.Dimension - 1) sb.Append(", ");
            }
            sb.Append("]");
            if (i < data.Vertices.Count - 1) sb.Append(",");
            sb.AppendLine();
        }
        sb.AppendLine("  ],");

        // --- facets ---
        sb.AppendLine("  \"facets\": [");
        for (int i = 0; i < data.Facets.Count; i++)
        {
            var facet = data.Facets[i];
            sb.AppendLine("    {");
            
            // indices
            sb.Append("      \"indices\": [");
            for (int j = 0; j < data.Dimension; j++)
            {
                sb.Append(facet.Indices[j]);
                if (j < data.Dimension - 1) sb.Append(", ");
            }
            sb.AppendLine("],");

            // normal
            sb.Append("      \"normal\": [");
            for (int j = 0; j < data.Dimension; j++)
            {
                sb.Append(facet.Normal.Coordinates[j].ToString(CultureInfo.InvariantCulture));
                if (j < data.Dimension - 1) sb.Append(", ");
            }
            
            if (facet.Center != null)
            {
                sb.AppendLine("],");
            }
            else
            {
                sb.AppendLine("]");
            }
            // If facet.Center is not null, write its contents.
            if (facet.Center != null)
            {
                sb.Append("      \"center\": [");
                for (int j = 0; j < data.Dimension; j++)
                {
                    sb.Append(facet.Center.Coordinates[j].ToString(CultureInfo.InvariantCulture));
                    if (j < data.Dimension - 1) sb.Append(", ");
                }
                sb.AppendLine("]");
            }

            sb.Append("    }");
            if (i < data.Facets.Count - 1) sb.Append(",");
            sb.AppendLine();
        }
        sb.AppendLine("  ]");
        
        sb.AppendLine("}");

        return sb.ToString();
    }
}
