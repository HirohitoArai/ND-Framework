using Geometry3D;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

//Perform 3D Boolean operations
public class BooleanManager3D : MonoBehaviour
{
    //Variables for retrieving data from the drawing method
    public RenderMesh3D MesheA;
    public RenderMesh3D MesheB;

    //Variables for retrieving data from the drawing method
    private FacetData3D[] _dataA;
    private FacetData3D[] _dataB;

    //Tolerance and its reciprocal
    const float EPS = 1e-4f;
    const float INV_EPS = 1 / EPS;

    //The size of the space division cell and its reciprocal
    public float CELLSIZE = 0.2f;
    private float invCellSize;

    private HashSet<FacetData3D> _AinB = new();//Facets of A that are inside the mesh of B
    private HashSet<FacetData3D> _AoutB = new();//Facets of A that lie outside the mesh of B
    private HashSet<FacetData3D> _BinA = new();//Facets of B that are inside the mesh of A
    private HashSet<FacetData3D> _BoutA = new();//Facets of B that lie outside the mesh of A

    //Calculate the union
    [ContextMenu("Run UnionBoolean")]
    private void UnionBoolean()
    {
        CutMeshes();
        var unionMeshes = new List<FacetData3D>();
        unionMeshes.AddRange(_AoutB);
        unionMeshes.AddRange(_BoutA);
        SaveResultsToJSON(unionMeshes,"Union");
    }

    //Calculate the intersection
    [ContextMenu("Run IntersectionBoolean")]
    private void IntersectionBoolean()
    {
        CutMeshes();
        var intersectionMeshes = new List<FacetData3D>();
        intersectionMeshes.AddRange(_AinB);
        intersectionMeshes.AddRange(_BinA);
        SaveResultsToJSON(intersectionMeshes,"Intersection");
    }

    //Calculate the set difference
    [ContextMenu("Run DifferenceBoolean(A cut B)")]
    private void DifferenceBoolean()
    {
        CutMeshes();
        var differenceMeshes = new List<FacetData3D>();

        //Flip Normals
        foreach (var meshOfA_in_B in _AinB) differenceMeshes.Add(new FacetData3D(meshOfA_in_B.FacetVert, -meshOfA_in_B.FacetNormal));
        differenceMeshes.AddRange(_BoutA);
        SaveResultsToJSON(differenceMeshes,"Difference");
    }

    //Cutting facets with each other's facets
    private void CutMeshes()
    {
        invCellSize = 1 / CELLSIZE;
        _AinB.Clear();
        _AoutB.Clear();
        _BinA.Clear();
        _BoutA.Clear();

        _dataA = MesheA.GetBooleanMeshData();
        _dataB = MesheB.GetBooleanMeshData();
        
        var currentAMax = _dataA[0].FacetVert[0];
        var currentAMin = _dataA[0].FacetVert[0];
        foreach (var data in _dataA)
        {
            MaxMin(data, currentAMax, currentAMin, out Vector3 max, out Vector3 min);
            currentAMax = max;
            currentAMin = min;
        }
        var meshA_AABB = new AABB3D(currentAMax, currentAMin);

        var currentBMax = _dataB[0].FacetVert[0];
        var currentBMin = _dataB[0].FacetVert[0];
        foreach (var data in _dataB)
        {
            MaxMin(data, currentBMax, currentBMin, out Vector3 max, out Vector3 min);
            currentBMax = max;
            currentBMin = min;
        }
        var meshB_AABB = new AABB3D(currentBMax, currentBMin);

        var facetDictionaryB = RegisterFacetToCell(_dataB);//Register which cells each facet of one mesh exists in
        var filteredPairs = new HashSet<(FacetData3D facetA, FacetData3D facetB)>();//Pairs filtered by AABB (HashSet that prevents duplicates)

        //Filter by AABB
        foreach (var facetA in _dataA)
        {
            AABB3D aabbA = BuildAABB(facetA);
            Vector3Int maxCell = GetCellIndex(aabbA.Max);
            Vector3Int mimCell = GetCellIndex(aabbA.Min);
            for (int x = mimCell.x; x <= maxCell.x; x++)
            for (int y = mimCell.y; y <= maxCell.y; y++)
            for (int z = mimCell.z; z <= maxCell.z; z++)
            {
                Vector3Int cellIndex = new Vector3Int(x, y, z);
                if (facetDictionaryB.TryGetValue(cellIndex, out var facetsB))
                { 
                    foreach (var facetB in facetsB)
                    { 
                        if (AABBCheck(aabbA, facetB.Item2))
                        {
                            filteredPairs.Add((facetA, facetB.Item1));
                        } 
                    }
                }
            }
        }
       
        var cuttedFacetsA = new HashSet<FacetData3D>(_dataA);//The final chopped facet of Mesh A (Facet A)
        var cuttedFacetsB = new HashSet<FacetData3D>(_dataB);//The final chopped facet of Mesh B (Facet B)
        var cuttedFacetsDictonaryA = new Dictionary<FacetData3D, List<FacetData3D>>();//A dictionary of facet B that slices facet A
        var cuttedFacetsDictonaryB = new Dictionary<FacetData3D, List<FacetData3D>>();//Dictionary of facet A chopping facet B
        var crossedFacetsA = new List<FacetData3D>();//Facet A actually intersects with facet B
        var crossedFacetsB = new List<FacetData3D>();//Facet B actually intersects with facet A

        //Actual intersection or filtering
        foreach (var pair in filteredPairs)
        {
            FacetData3D facetA = pair.facetA;
            FacetData3D facetB = pair.facetB;

            //Do they actually intersect
            if (IsFacetCrossing(facetA,facetB,out List<Vector3> intercects))
            {
                //If facetA is not registered, create a new key
                if (!cuttedFacetsDictonaryA.ContainsKey(facetA))
                {
                    cuttedFacetsDictonaryA[facetA] = new List<FacetData3D>();
                    cuttedFacetsA.Remove(facetA);//It will be chopped up so delete it now (I'll add the chopped up version later)
                    crossedFacetsA.Add(facetA); 
                }

                //If facetB is not registered, create a new key
                if (!cuttedFacetsDictonaryB.ContainsKey(facetB))
                {
                    cuttedFacetsDictonaryB[facetB] = new List<FacetData3D>();
                    cuttedFacetsB.Remove(facetB);//It will be chopped up so delete it now (I'll add the chopped up version later)
                    crossedFacetsB.Add(facetB);
                }
                cuttedFacetsDictonaryA[facetA].Add(facetB);//Register facetB as the face to cut facetA
                cuttedFacetsDictonaryB[facetB].Add(facetA);//Register facetA as the face to cut facetB
            }
        }

        //Slice off facets that are actually found to intersect
        foreach (var facetA in crossedFacetsA) ArrengeMesh(facetA, cuttedFacetsDictonaryA, cuttedFacetsA);
        foreach (var facetB in crossedFacetsB) ArrengeMesh(facetB, cuttedFacetsDictonaryB, cuttedFacetsB);
        
        //Registering the cut facets to the spatial cells for inside/outside determination
        var cuttedFacetDictionaryA = RegisterFacetToCell(_dataA);
        var cuttedFacetDictionaryB = RegisterFacetToCell(_dataB);

        //Divide facets into internal and external
        GroupByContainment(cuttedFacetsA, cuttedFacetDictionaryB,meshB_AABB, _AinB, _AoutB);
        GroupByContainment(cuttedFacetsB, cuttedFacetDictionaryA,meshA_AABB, _BinA, _BoutA);
    }

    private List<Vector3> _sideP = new ();//A list of the vertices of the facet that are on one side of the cross section
    private List<Vector3> _sideN = new ();//A list of vertices on the side opposite side P
    private List<Vector3> _onPlane = new ();//Points on the cross section
    private List<FacetData3D> _currentMeshList = new ();//Facets being cut into pieces
    private List<FacetData3D> _nextMeshList = new ();//A list that temporarily stores facets immediately after cutting. Adds them to _currentMeshList each time the loop runs
    private void ArrengeMesh(FacetData3D mesh, Dictionary<FacetData3D, List<FacetData3D>> cuttedMeshesDictonary, HashSet<FacetData3D> cuttedMeshes)//Cutting the mesh
    {
        var cuttingPlanes = cuttedMeshesDictonary[mesh];
        _currentMeshList.Clear();
        _currentMeshList.Add(mesh);

        Vector3 meshNormal = mesh.FacetNormal;
        foreach (var cuttingPlane in cuttingPlanes)
        {
            _nextMeshList.Clear();
            foreach (var currentMesh in _currentMeshList)
            {
                //Since the test is performed on the new cut facet, a test must be performed again
                if (!IsFacetCrossing(currentMesh, cuttingPlane, out List<Vector3> intersects))
                { 
                    //If they do not intersect, just add them
                    _nextMeshList.Add(currentMesh);
                    continue;
                }

                _sideP.Clear();
                _sideN.Clear();
                _onPlane.Clear();
                //Check which side of the cut each vertex of the triangle is on
                foreach (var vertex in currentMesh.FacetVert)
                {
                    float dist = Vector3.Dot(vertex - cuttingPlane.FacetVert[0], cuttingPlane.FacetNormal);

                    if (dist > EPS)
                    {
                        _sideP.Add(vertex);
                    }
                    else if (dist < -EPS)
                    {
                        _sideN.Add(vertex);
                    }
                    else
                    {
                        // If a vertex is very close to the cutting plane, the vertex of that facet is considered to be a vertex on the cross section
                        _onPlane.Add(vertex);
                    }
                }

                //If the cross section touches an edge or vertex, add it to the list without cutting it.
                if (_onPlane.Count >= 2 || _sideP.Count == 0 || _sideN.Count == 0)
                {
                    _nextMeshList.Add(currentMesh);
                    continue;
                }
                //What to do if the cross section passes through a vertex and splits it into two
                if (_onPlane.Count == 1)
                { 
                    var mergedIntersects = new List<Vector3>();
                    foreach (var intersect in intersects)
                    { 
                        float dist = (intersect - _onPlane[0]).sqrMagnitude;
                        if (dist < EPS * EPS) mergedIntersects.Add(_onPlane[0]);
                        else mergedIntersects.Add(intersect);
                    }

                    _nextMeshList.Add(new FacetData3D(new Vector3[] { mergedIntersects[0], mergedIntersects[1], _sideP[0] }, currentMesh.FacetNormal));
                    _nextMeshList.Add(new FacetData3D(new Vector3[] { mergedIntersects[0], mergedIntersects[1], _sideN[0] }, currentMesh.FacetNormal));
                }
                else if (_sideP.Count == 1)//Normal cutting process
                {
                    TessellateFacet(_sideP, _sideN, meshNormal, intersects, _nextMeshList);
                }
                else if (_sideN.Count == 1)
                {
                    TessellateFacet(_sideN, _sideP, meshNormal, intersects, _nextMeshList);
                }
            }
            var tempList = _currentMeshList;
            _currentMeshList = _nextMeshList;
            _nextMeshList = tempList;//Add the split facet to the next loop
        }

        foreach(var devidedMesh in _currentMeshList) cuttedMeshes.Add(devidedMesh);
    }

    //Grouping split facets
    private void GroupByContainment(HashSet<FacetData3D> cuttedMeshes, Dictionary<Vector3Int, List<(FacetData3D, AABB3D)>> meshDictionary, AABB3D aabb, HashSet<FacetData3D> XinY, HashSet<FacetData3D> XoutY)
    {
        //Intersection event, coordinates rounded to a tolerance and defined as an int key
        var crossEvents = new Dictionary<int, List<float>>();

        foreach (var cuttedMesh in cuttedMeshes)
        {
            crossEvents.Clear();
            var center = cuttedMesh.FacetCenter;
            var end = new Vector3(aabb.Max.x + 1.0f, center.y, center.z);//Increase the length of the lei by 1 for insurance purposes.
            var startCell = GetCellIndex(center);
            var endCell = GetCellIndex(end);

            //Check from the start cell to the end cell
            for (int x = startCell.x; x <= endCell.x; x++)
            {
                var currentCell = new Vector3Int(x, startCell.y, startCell.z);
                //Find the facets belonging to the current cell from the current cell
                if (meshDictionary.TryGetValue(currentCell, out var meshes))
                {
                    foreach (var mesh in meshes)
                    {
                        //If the ray crosses, register for the crossing event
                        if (IsRayCrossingFacet(center, end, mesh.Item1, out Vector3 intersection))
                        {
                            int keyX = Mathf.RoundToInt(intersection.x * INV_EPS);
                            float normalX = mesh.Item1.FacetNormal.x;
                            //Very close intersections are treated as one event
                            if (!crossEvents.ContainsKey(keyX))
                            {
                                crossEvents[keyX] = new List<float>();
                            }
                            crossEvents[keyX].Add((normalX));
                        }
                    }
                }
            }
        
            //Determine whether it is a penetration case or a contact case based on the distribution of the sign of the inner product
            int finalIntersectionCount = 0;
            foreach (var pair in crossEvents)
            {
                var hits = pair.Value; 

                if (hits.Count == 1)
                {
                    finalIntersectionCount++;
                }
                else 
                {
                    bool hasPositive = false;
                    bool hasNegative = false;
                    foreach (var nx in hits) 
                    {
                        if (nx>= 0) hasPositive = true;
                        if (nx< 0) hasNegative = true;
                    }

                    if (!(hasPositive && hasNegative))
                    {
                        finalIntersectionCount++;
                    }
                }
            }
            //If the number of crossings is even, it is outside, if it is odd, it is inside
            if (finalIntersectionCount % 2 == 0)
            {
                XoutY.Add(cuttedMesh);
            } else {
                XinY.Add(cuttedMesh);
            }
        }
    }

    private AABB3D BuildAABB(FacetData3D facet)
    {
        MaxMin(facet,facet.FacetVert[0],facet.FacetVert[0], out Vector3 max, out Vector3 min);
        return new AABB3D(max, min);
    }
    private void MaxMin(FacetData3D facet,Vector3 defaultMax,Vector3 defaultMin, out Vector3 max, out Vector3 min)
    {
        Vector3 Max = defaultMax;
        Vector3 Min = defaultMin;
        for (int j = 0; j < facet.FacetVert.Length; j++)
        {
            Max = Vector3.Max(Max, facet.FacetVert[j]);
            Min = Vector3.Min(Min, facet.FacetVert[j]);
        }
        max = Max;
        min = Min;
    }
    private bool AABBCheck(AABB3D a, AABB3D b)
    {
        // Checking for overlaps on each axis
        bool overlapX = a.Min.x <= b.Max.x && a.Max.x >= b.Min.x;
        bool overlapY = a.Min.y <= b.Max.y && a.Max.y >= b.Min.y;
        bool overlapZ = a.Min.z <= b.Max.z && a.Max.z >= b.Min.z;

        // If they do not overlap on all three axes, they do not intersect.
        if (!overlapX || !overlapY || !overlapZ) return false;
        return true;
    }

    //Registering facets to spatial cells
    private Dictionary<Vector3Int, List<(FacetData3D,AABB3D)>> RegisterFacetToCell(FacetData3D[] facets)
    { 
        // Count the number of facets in each cell
        var cellCounts = new Dictionary<Vector3Int, int>();
        foreach (var facet in facets)
        {
            var aabb = BuildAABB(facet);
            Vector3Int maxCell = GetCellIndex(aabb.Max);
            Vector3Int minCell = GetCellIndex(aabb.Min);
            for (int x = minCell.x; x <= maxCell.x; x++)
            for (int y = minCell.y; y <= maxCell.y; y++)
            for (int z = minCell.z; z <= maxCell.z; z++)
            {
                var cellIndex = new Vector3Int(x, y, z);
                if (!cellCounts.ContainsKey(cellIndex))
                {
                    cellCounts[cellIndex] = 0;
                }
                cellCounts[cellIndex]++;
            }
        }

        //Build dictionaries and lists with the correct capacity (to prevent GC)
        var facetDictionary = new Dictionary<Vector3Int, List<(FacetData3D, AABB3D)>>(cellCounts.Count);
        // Create a list with the correct capacity in advance based on the dictionary keys
        foreach (var pair in cellCounts)
        {
            facetDictionary.Add(pair.Key, new List<(FacetData3D, AABB3D)>(pair.Value));
        }

        //Storing Facets
        foreach (var facet in facets)
        {
            var aabb = BuildAABB(facet);
            Vector3Int maxCell = GetCellIndex(aabb.Max);
            Vector3Int mimCell = GetCellIndex(aabb.Min);
            for (int x = mimCell.x; x <= maxCell.x; x++)
            for (int y = mimCell.y; y <= maxCell.y; y++)
            for (int z = mimCell.z; z <= maxCell.z; z++)
            {
                var cellIndex = new Vector3Int(x, y, z);
                facetDictionary[cellIndex].Add((facet, aabb));
            }
        }
        return facetDictionary;
    }

    List<Vector3> _targetIntercects = new ();
    List<Vector3> _cutterIintercects = new ();
    //Determine if facets actually intersect
    private bool IsFacetCrossing(FacetData3D targetMesh,FacetData3D cutterMesh,out List<Vector3> intercects)
    {
        _targetIntercects.Clear();
        _cutterIintercects.Clear();
        CulculateEdgePoint(targetMesh, cutterMesh.FacetNormal, cutterMesh.FacetVert[0], _targetIntercects);
        CulculateEdgePoint(cutterMesh, targetMesh.FacetNormal, targetMesh.FacetVert[0], _cutterIintercects);

        intercects = _targetIntercects;
        if (_targetIntercects.Count < 2 || _cutterIintercects.Count < 2) return false;
        Vector3 ab00 = _cutterIintercects[0] - _targetIntercects[0];
        Vector3 ab01 = _cutterIintercects[1] - _targetIntercects[1];
        Vector3 ab10 = _cutterIintercects[0] - _targetIntercects[1];
        Vector3 ab11 = _cutterIintercects[1] - _targetIntercects[0];
        if (Vector3.Dot(ab00, ab01) < -EPS || Vector3.Dot(ab10, ab11) < -EPS)
        { 
            return true;
        } 
        return false;
    }
    //Finding the intersection of a facet and an infinite plane
    private void CulculateEdgePoint(FacetData3D mesh,Vector3 planeNormal, Vector3 planePoint, List<Vector3> intersects)
    {
        intersects.Clear();
        Vector3 p0 = mesh.FacetVert[0];
        Vector3 p1 = mesh.FacetVert[1];
        Vector3 p2 = mesh.FacetVert[2];
        if (TryCulculateIntercention(p0, p1, planeNormal, planePoint, out Vector3 intersection0)) intersects.Add(intersection0);
        if (TryCulculateIntercention(p0, p2, planeNormal, planePoint, out Vector3 intersection1)) intersects.Add(intersection1);
        if (TryCulculateIntercention(p1, p2, planeNormal, planePoint, out Vector3 intersection2)) intersects.Add(intersection2);
    }
    //Finding the intersection of an infinite plane and a line
    private bool TryCulculateIntercention(Vector3 start,Vector3 end, Vector3 planeNormal, Vector3 planePoint,out Vector3 intersection)
    { 
        intersection = Vector3.zero;
        Vector3 d = end - start;
        float denominator = Vector3.Dot(planeNormal, d);

        if (Mathf.Abs(denominator) < EPS) return false;
        float t = Vector3.Dot(planeNormal, planePoint - start) / denominator;

        if (t < 0f || t > 1f) return false; // Exclude outside the line
        intersection = start + t * d;
        return true;
    }
    //Cut the facet with an infinite plane and divide it into simplex parts (if it is three-dimensional, divide the facet into a set of triangles)
    private void TessellateFacet(List<Vector3> sideX, List<Vector3> sideY, Vector3 meshNormal, List<Vector3> intercects, List<FacetData3D> nextMeshList)
    {
        var simplex0 = new FacetData3D(new Vector3[] { sideX[0], intercects[0], intercects[1] }, meshNormal);
        nextMeshList.Add(simplex0);
        if (Vector3.Dot(Vector3.Cross(sideX[0] - intercects[0], intercects[0] - sideY[0]), meshNormal) < Mathf.Abs(EPS))
        {
            var simplex1 = new FacetData3D(new Vector3[] { sideY[0], intercects[0], intercects[1] }, meshNormal);
            var simplex2 = new FacetData3D(new Vector3[] { intercects[1], sideY[0], sideY[1] }, meshNormal);
            nextMeshList.Add(simplex1);
            nextMeshList.Add(simplex2);
        }
        else
        {
            var simplex1 = new FacetData3D(new Vector3[] { sideY[0], intercects[0], intercects[1] }, meshNormal);
            var simplex2 = new FacetData3D(new Vector3[] { intercects[0], sideY[0], sideY[1] }, meshNormal);
            nextMeshList.Add(simplex1);
            nextMeshList.Add(simplex2);
        }
    }
    private bool IsRayCrossingFacet(Vector3 rayStart,Vector3 rayEnd, FacetData3D facet,out Vector3 intersection)
    {
        intersection = Vector3.zero;
        //Find the intersection of a ray with an infinite plane parallel to the facet
        if (!TryCulculateIntercention(rayStart,rayEnd,facet.FacetNormal,facet.FacetVert[0],out Vector3 p)) return false;

        //Determine whether an intersection is inside or outside using varicentric coordinates
        Vector3 v0 = facet.FacetVert[1] - facet.FacetVert[0];
        Vector3 v1 = facet.FacetVert[2] - facet.FacetVert[0];
        Vector3 v2 = p - facet.FacetVert[0];

        float dot00 = Vector3.Dot(v0, v0);
        float dot01 = Vector3.Dot(v0, v1);
        float dot02 = Vector3.Dot(v0, v2);
        float dot11 = Vector3.Dot(v1, v1);
        float dot12 = Vector3.Dot(v1, v2);

        float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;
        intersection = p;

        return ((u >= -EPS) && (v >= -EPS) && (u + v <= 1f + EPS));
    }
    //Get spatial cell index from coordinates
    private Vector3Int GetCellIndex(Vector3 P)
    {
        int x = (int)Math.Floor(P.x * invCellSize);
        int y = (int)Math.Floor(P.y * invCellSize);
        int z = (int)Math.Floor(P.z * invCellSize);

        return new Vector3Int(x, y, z);
    }
    private Vector3Int QuantizeVector3(Vector3 vertex)
    {
        // Scale the coordinates by the inverse error (INV_EPS) and round to integers
        return new Vector3Int(
            Mathf.RoundToInt(vertex.x * INV_EPS),
            Mathf.RoundToInt(vertex.y * INV_EPS),
            Mathf.RoundToInt(vertex.z * INV_EPS)
        );
    }

    private void SaveResultsToJSON(List<FacetData3D> mixedMeshes, string operationName)
    {
        Debug.Log($"Saving boolean results for {operationName} to JSON file...");
        if (mixedMeshes.Count > 0)
        {
            //Vertex deduplication and indexing logic
            MeshData3D _dataA = ConvertToMeshData(mixedMeshes);

            //Serialize a ConvexMeshData3D object directly to a JSON string
            string jsonA = JsonUtility.ToJson(_dataA, true); 

            Destroy(_dataA);
            string fileName = $"ResultMesh_{operationName}.json";
            string path = Path.Combine(Application.dataPath, fileName);
            try
            {
                File.WriteAllText(path, jsonA);
                Debug.Log($"Successfully saved result to: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save JSON for {operationName}: {e.Message}");
            }
        }
    }

    //Export as mesh data
    private MeshData3D ConvertToMeshData(List<FacetData3D> cuttedMeshes)
    {
        var vertexIndexLookup = new Dictionary<Vector3Int, int>();
        var finalVertices = new List<Vector3>();
        int nextVertexIndex = 0;

        //Merging vertices and setting indices
        foreach (var facet in cuttedMeshes)
        {
            foreach (var currentVertex in facet.FacetVert)
            {
                // Quantize vertices to Vector3Int
                Vector3Int key = QuantizeVector3(currentVertex); 
                if (!vertexIndexLookup.ContainsKey(key))
                {
                    finalVertices.Add(currentVertex); 
                    vertexIndexLookup.Add(key, nextVertexIndex);
                    nextVertexIndex++;
                }
            }
        }

        //Create a face index by the index associated with the vertex
        var finalFacets = new List<FacetInfo3D>();
    
        foreach (var facet in cuttedMeshes)
        {
            int[] faceIndices = new int[3];
            for (int i = 0; i < 3; i++)
            {
                Vector3 currentVertex = facet.FacetVert[i];
                Vector3Int key = QuantizeVector3(currentVertex);
            
                // Enter a vertex in the dictionary and get the corresponding index
                if (vertexIndexLookup.TryGetValue(key, out int foundIndex))
                {
                    faceIndices[i] = foundIndex;
                }
            }
            var newFace = new FacetInfo3D(new FacetIdx3D(faceIndices[0], faceIndices[1], faceIndices[2]), facet.FacetNormal,facet.FacetCenter);
            finalFacets.Add(newFace);
        }
        // Creating the final mesh data
        MeshData3D meshData = ScriptableObject.CreateInstance<MeshData3D>();
        meshData.Vertices = finalVertices.ToArray();
        meshData.SurfaceFaces = finalFacets.ToArray();

        return meshData;
    }
    

    #region Benchmark Tools
    private const int NUMBER_OF_EXECUTIONS = 3;

    [ContextMenu("Run ALL Benchmarks")]
    private void RunAllBenchmarks()
    {
        UnityEngine.Debug.Log("--- Starting Benchmark Suite ---", this.gameObject);
        RunTestForOperation("UnionBoolean");
        RunTestForOperation("IntersectionBoolean");
        RunTestForOperation("DifferenceBoolean");

        UnityEngine.Debug.Log("--- Benchmark Suite Finished ---");
    }
    private void RunTestForOperation(string methodName)
    {
        if (MesheA == null || MesheB == null)
        {
            UnityEngine.Debug.LogError("Benchmark Error: MeshesA or MeshesB is not set.");
            return;
        }

        var results = new System.Collections.Generic.List<double>();
        var stopwatch = new System.Diagnostics.Stopwatch();

        UnityEngine.Debug.Log($"--- Measuring operation: {methodName} ---");
        this.SendMessage(methodName); 

        for (int i = 0; i < NUMBER_OF_EXECUTIONS; i++)
        {
            stopwatch.Reset();
            stopwatch.Start();

            this.SendMessage(methodName);

            stopwatch.Stop();
            results.Add(stopwatch.Elapsed.TotalMilliseconds);
        }
        PrintBenchmarkResults(methodName, results);
    }
    private static void PrintBenchmarkResults(string benchmarkName, System.Collections.Generic.List<double> results)
    {
        double averageTime = results.Average();
        double minTime = results.Min();
        double maxTime = results.Max();
        double sumOfSquares = results.Select(val => (val - averageTime) * (val - averageTime)).Sum();
        double stdDev = Mathf.Sqrt((float)(sumOfSquares / results.Count));

        UnityEngine.Debug.Log($"--- Benchmark Results: {benchmarkName} ---");
        UnityEngine.Debug.Log($"Total Executions: {results.Count}");
        UnityEngine.Debug.Log($"Average Time: {averageTime:F4} ms");
        //UnityEngine.Debug.Log($"Min Time: {minTime:F4} ms");
        //UnityEngine.Debug.Log($"Max Time: {maxTime:F4} ms");
        //UnityEngine.Debug.Log($"Standard Deviation (scattering): {stdDev:F4} ms");
        UnityEngine.Debug.Log("------------------------------------");
    }
    #endregion
}

