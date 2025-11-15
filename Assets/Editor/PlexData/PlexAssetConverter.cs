using Geometry4D;
using Geometry3D;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class PlexAssetConverter
{
    //-------------------------------------------------
    //ScriptableObject2PlexData
    //-------------------------------------------------
    private const string ExportPlexMenuPath = "Assets/Export as plex file";
    [MenuItem(ExportPlexMenuPath, false, 12)]
    private static void ExportSelectedAssetToPlex()
    {
        var selectedObject = Selection.activeObject;
        if (selectedObject == null)
        {
            Debug.LogError("Input MeshData asset is null.");
            return;
        }

        PlexData plexData = null;
        string assetName = selectedObject.name;
        if (selectedObject is MeshData4D meshAsset4D)
        {
            // --- For 4D assets ---
            Debug.Log($"Selected asset is a 4D Mesh. Using 4D converter...");
            plexData = ScriptableObject2PlexData.FromUnityScriptableObject4D(meshAsset4D);
        }
        else if (selectedObject is MeshData3D meshAsset3D)
        {
            // --- For 3D assets ---
            Debug.Log($"Selected asset is a 3D Mesh. Using 3D converter...");
            plexData = ScriptableObject2PlexData.FromUnityScriptableObject3D(meshAsset3D); 
        }

        if (plexData == null)
        {
            Debug.LogWarning("Selected asset is not a supported mesh type.");
            return;
        }

        string initialFileName = $"{assetName}.plex";
        string path = EditorUtility.SaveFilePanel("Export as plex file", "", initialFileName, "plex");

        //Continue only if path is selected
        if (!string.IsNullOrEmpty(path))
        {
            Debug.Log($"Exporting '{assetName}' to '{path}'...");
            try
            {
                //Write to a binary file with PlexParser
                PlexParser.Write(path, plexData);
                    
                Debug.Log($"<color=green>Successfully exported '{assetName}' to .plex file!</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to export .plex file: {e.Message}");
            }
        }
        
    }
    [MenuItem(ExportPlexMenuPath, true)]
    private static bool ValidateExportSelectedAssetToPlex()
    {
        if (Selection.activeObject == null) return false;
        return Selection.activeObject is MeshData4D || Selection.activeObject is MeshData3D;
    }
    //-------------------------------------------------
    //ScriptableObject2PlexData
    //-------------------------------------------------





    //-------------------------------------------------
    //PlexData2ScriptableObject
    //-------------------------------------------------
    private const string ImportPlexMenuPath = "Assets/Import from plex file";
    [MenuItem(ImportPlexMenuPath, false, 13)]
    private static void ImportPlexFileAsAsset()
    {
        string path = EditorUtility.OpenFilePanel("Import .plex file", "", "plex");

        if (string.IsNullOrEmpty(path)) return;

        Debug.Log($"Importing from '{path}'...");
        try
        {
            PlexData plexData = PlexParser.Read(path);
            if (plexData == null)
            {
                Debug.LogError("Failed to parse .plex file.");
                return;
            }

            //Prepare the save path and file name
            string directory = "Assets/";
            string fileName = $"{Path.GetFileNameWithoutExtension(path)}.asset";
            string assetPath = EditorUtility.SaveFilePanelInProject("Save Imported Asset", fileName, "asset", "", directory);
            
            if (string.IsNullOrEmpty(assetPath)) return;

            //Generate and save appropriate assets by dividing the cases based on the number of dimensions
            switch (plexData.Dimension)
            {
                case 4:
                    // Generate 4D assets
                    MeshData4D mesh4D = PlexData2ScriptableObject.ToUnityScriptableObject4D(plexData);
                    if (mesh4D != null)
                    {
                        AssetDatabase.CreateAsset(mesh4D, assetPath);
                        Debug.Log($"<color=green>Successfully imported as a 4D Mesh Asset!</color>");
                    }
                    break;
                
                case 3:
                    // Generate 3D assets
                    MeshData3D mesh3D = PlexData2ScriptableObject.ToUnityScriptableObject3D(plexData);
                    if (mesh3D != null)
                    {
                        AssetDatabase.CreateAsset(mesh3D, assetPath);
                        Debug.Log($"<color=green>Successfully imported as a 3D Mesh Asset!</color>");
                    }
                    break;
                
                default:
                    Debug.LogError($"Unsupported dimension ({plexData.Dimension}) for import. Cannot create a ScriptableObject.");
                    break;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to import .plex file: {e.Message}\n{e.StackTrace}");
        }
    }
    //-------------------------------------------------
    //PlexData2ScriptableObject
    //-------------------------------------------------
}


public static class ScriptableObject2PlexData
{
    public static PlexData FromUnityScriptableObject3D(MeshData3D assetToConvert, PlexPrecision precision = PlexPrecision.Single, string softwareName = "Unity Plex Exporter")
    {
        var plexData = new PlexData();

        //Transfer of information equivalent to META chunks
        plexData.Dimension = 3;
        plexData.Precision = precision;
        plexData.SoftwareName = softwareName;
        // CreationTime and ModifiedTime are automatically set in the PlexData constructor

        //Vertex data (VERT/VERD) conversion
        // Vector4[] to List<PlexVector>
        if (assetToConvert.Vertices != null)
        {
            plexData.Vertices = new List<PlexVector>(assetToConvert.Vertices.Length);
            foreach (var unityVertex in assetToConvert.Vertices)
            {
                var plexVector = new PlexVector(plexData.Dimension);
                plexVector.Coordinates[0] = unityVertex.x;
                plexVector.Coordinates[1] = unityVertex.y;
                plexVector.Coordinates[2] = unityVertex.z;
                plexData.Vertices.Add(plexVector);
            }
        }
        plexData.VertexCount = (ulong)plexData.Vertices.Count;

        //Conversion of facet-related data (FACE, NORM, CENT)
        if (assetToConvert.SurfaceFaces != null)
        {
            plexData.Facets = new List<PlexFacet>(assetToConvert.SurfaceFaces.Length);
            foreach (var unityFacetInfo in assetToConvert.SurfaceFaces)
            {
                if (unityFacetInfo.FacetNormal.sqrMagnitude < 0.0001f) continue;

                var plexFacet = new PlexFacet(plexData.Dimension);

                // Index (for FACE chunk)
                plexFacet.Indices[0] = (uint)unityFacetInfo.FacetIdx.V0;
                plexFacet.Indices[1] = (uint)unityFacetInfo.FacetIdx.V1;
                plexFacet.Indices[2] = (uint)unityFacetInfo.FacetIdx.V2;

                
                // Normals (for NORM/NORMD chunks)
                // Normal is already initialized in the PlexFacet constructor
                plexFacet.Normal = new PlexVector(plexData.Dimension); 
                plexFacet.Normal.Coordinates[0] = unityFacetInfo.FacetNormal.x;
                plexFacet.Normal.Coordinates[1] = unityFacetInfo.FacetNormal.y;
                plexFacet.Normal.Coordinates[2] = unityFacetInfo.FacetNormal.z;
                
                // Center of gravity (for CENT/CENTD chunks)
                // Converts center of gravity data if it exists in the ScriptableObject
                plexFacet.Center = new PlexVector(plexData.Dimension);
                plexFacet.Center.Coordinates[0] = unityFacetInfo.FacetCenter.x;
                plexFacet.Center.Coordinates[1] = unityFacetInfo.FacetCenter.y;
                plexFacet.Center.Coordinates[2] = unityFacetInfo.FacetCenter.z;
                
                plexData.Facets.Add(plexFacet);
            }
        }

        Debug.Log($"Converted {assetToConvert.name} to PlexData. Vertices: {plexData.Vertices.Count}, Facets: {plexData.Facets.Count}");
        return plexData;
    }
    public static PlexData FromUnityScriptableObject4D(MeshData4D assetToConvert, PlexPrecision precision = PlexPrecision.Single, string softwareName = "Unity Plex Exporter")
    {
        var plexData = new PlexData();

        //Transfer of information equivalent to META chunks
        plexData.Dimension = 4;
        plexData.Precision = precision;
        plexData.SoftwareName = softwareName;
        // CreationTime and ModifiedTime are automatically set in the PlexData constructor.

        //Vertex data (VERT/VERD) conversion
        // Vector4[] to List<PlexVector>
        if (assetToConvert.Vertices != null)
        {
            plexData.Vertices = new List<PlexVector>(assetToConvert.Vertices.Length);
            foreach (var unityVertex in assetToConvert.Vertices)
            {
                var plexVector = new PlexVector(plexData.Dimension);
                plexVector.Coordinates[0] = unityVertex.x;
                plexVector.Coordinates[1] = unityVertex.y;
                plexVector.Coordinates[2] = unityVertex.z;
                plexVector.Coordinates[3] = unityVertex.w;
                plexData.Vertices.Add(plexVector);
            }
        }
        plexData.VertexCount = (ulong)plexData.Vertices.Count;

        //Conversion of facet-related data (FACE, NORM, CENT)
        if (assetToConvert.SurfaceFaces != null)
        {
            plexData.Facets = new List<PlexFacet>(assetToConvert.SurfaceFaces.Length);
            foreach (var unityFacetInfo in assetToConvert.SurfaceFaces)
            {
                if (unityFacetInfo.FacetNormal.sqrMagnitude < 0.0001f) continue;

                var plexFacet = new PlexFacet(plexData.Dimension);

                // Index (for FACE chunks)
                plexFacet.Indices[0] = (uint)unityFacetInfo.FacetIdx.V0;
                plexFacet.Indices[1] = (uint)unityFacetInfo.FacetIdx.V1;
                plexFacet.Indices[2] = (uint)unityFacetInfo.FacetIdx.V2;
                plexFacet.Indices[3] = (uint)unityFacetInfo.FacetIdx.V3;

                
                // Normals (for NORM/NORMD chunks)
                // Normal is already initialized in the PlexFacet constructor
                plexFacet.Normal = new PlexVector(plexData.Dimension); 
                plexFacet.Normal.Coordinates[0] = unityFacetInfo.FacetNormal.x;
                plexFacet.Normal.Coordinates[1] = unityFacetInfo.FacetNormal.y;
                plexFacet.Normal.Coordinates[2] = unityFacetInfo.FacetNormal.z;
                plexFacet.Normal.Coordinates[3] = unityFacetInfo.FacetNormal.w;
                
                // Center of gravity (for CENT/CENTD chunks)
                // Converts center of gravity data if it exists in the ScriptableObject
                plexFacet.Center = new PlexVector(plexData.Dimension);
                plexFacet.Center.Coordinates[0] = unityFacetInfo.FacetCenter.x;
                plexFacet.Center.Coordinates[1] = unityFacetInfo.FacetCenter.y;
                plexFacet.Center.Coordinates[2] = unityFacetInfo.FacetCenter.z;
                plexFacet.Center.Coordinates[3] = unityFacetInfo.FacetCenter.w;
                
                plexData.Facets.Add(plexFacet);
            }
        }

        Debug.Log($"Converted {assetToConvert.name} to PlexData. Vertices: {plexData.Vertices.Count}, Facets: {plexData.Facets.Count}");
        return plexData;
    }
}

public static class PlexData2ScriptableObject
{ 
    public static MeshData4D ToUnityScriptableObject4D(PlexData plexData)
    {
        if (plexData.Dimension != 4)
        {
            Debug.LogError($"Cannot convert to MeshData4D: PlexData dimension is {plexData.Dimension}, but expected 4.");
            return null;
        }
        var asset = ScriptableObject.CreateInstance<MeshData4D>();

        // Vertex transformation (List<PlexVector> -> Vector4[])
        asset.Vertices = new Vector4[plexData.Vertices.Count];
        for (int i = 0; i < plexData.Vertices.Count; i++)
        {
            var coords = plexData.Vertices[i].Coordinates;
            asset.Vertices[i] = new Vector4((float)coords[0], (float)coords[1], (float)coords[2], (float)coords[3]);
        }

        // Facet conversion (List<PlexFacet> -> FacetInfo4D[])
        asset.SurfaceFaces = new FacetInfo4D[plexData.Facets.Count];
        for (int i = 0; i < plexData.Facets.Count; i++)
        {
            var plexFacet = plexData.Facets[i];

            var facetIdx = new FacetIdx4D(
                (int)plexFacet.Indices[0], (int)plexFacet.Indices[1],
                (int)plexFacet.Indices[2], (int)plexFacet.Indices[3]
            );
        
            var facetNormal = new Vector4(
                (float)plexFacet.Normal.Coordinates[0], (float)plexFacet.Normal.Coordinates[1],
                (float)plexFacet.Normal.Coordinates[2], (float)plexFacet.Normal.Coordinates[3]
            );
            var facetCenter = Vector4.zero;
            if (plexFacet.Center != null)
            {
                facetCenter = new Vector4(
                    (float)plexFacet.Center.Coordinates[0], (float)plexFacet.Center.Coordinates[1],
                    (float)plexFacet.Center.Coordinates[2], (float)plexFacet.Center.Coordinates[3]
                );
            }

            asset.SurfaceFaces[i] = new FacetInfo4D(facetIdx, facetNormal, facetCenter);
        }
        return asset;
    }
    public static MeshData3D ToUnityScriptableObject3D(PlexData plexData)
    {
        if (plexData.Dimension != 3)
        {
            Debug.LogError($"Cannot convert to MeshData3D: PlexData dimension is {plexData.Dimension}, but expected 3.");
            return null;
        }

        var asset = ScriptableObject.CreateInstance<MeshData3D>();

        // Vertex transformation (List<PlexVector> -> Vector3[])
        asset.Vertices = new Vector3[plexData.Vertices.Count];
        for (int i = 0; i < plexData.Vertices.Count; i++)
        {
            var coords = plexData.Vertices[i].Coordinates;
            asset.Vertices[i] = new Vector3((float)coords[0], (float)coords[1], (float)coords[2]);
        }

        // Facet conversion (List<PlexFacet> -> FacetInfo4D[])
        asset.SurfaceFaces = new FacetInfo3D[plexData.Facets.Count];
        for (int i = 0; i < plexData.Facets.Count; i++)
        {
            var plexFacet = plexData.Facets[i];

            var facetIdx = new FacetIdx3D(
                (int)plexFacet.Indices[0], 
                (int)plexFacet.Indices[1],
                (int)plexFacet.Indices[2]
            );
        
            var facetNormal = new Vector3(
                (float)plexFacet.Normal.Coordinates[0], 
                (float)plexFacet.Normal.Coordinates[1],
                (float)plexFacet.Normal.Coordinates[2]
            );
            var facetCenter = Vector3.zero;
            if (plexFacet.Center != null)
            {
                facetCenter = new Vector3(
                    (float)plexFacet.Center.Coordinates[0], 
                    (float)plexFacet.Center.Coordinates[1],
                    (float)plexFacet.Center.Coordinates[2]
                );
            }
        
            asset.SurfaceFaces[i] = new FacetInfo3D(facetIdx, facetNormal, facetCenter);
        }
        return asset;
    }
}