using GeometricAlgebra4D;
using Geometry4D;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using UnityEngine;

//Perform 4D Boolean operations
public class BooleanManager4D : MonoBehaviour
{
    //Variables for retrieving data from the drawing method
    public RenderMesh4D MesheA;
    public RenderMesh4D MesheB;

    //Variables for retrieving data from the drawing method
    private FacetData4D[] _dataA;
    private FacetData4D[] _dataB;

    //Tolerance and its reciprocal
    const float EPS = 1e-4f;
    const float INV_EPS = 1 / EPS;

    //The size of the space division cell and its reciprocal
    public float CELLSIZE = 0.3f;
    private float INV_CELLSIZE;

    private HashSet<FacetData4D> _AinB = new();//Facets of A that are inside the mesh of B
    private HashSet<FacetData4D> _AoutB = new();//Facets of A that lie outside the mesh of B
    private HashSet<FacetData4D> _BinA = new();//Facets of B that are inside the mesh of A
    private HashSet<FacetData4D> _BoutA = new();//Facets of B that lie outside the mesh of A

    //Calculate the union
    [ContextMenu("Run UnionBoolean")]
    private void UnionBoolean()
    {
        CutMeshes();
        var unionMeshes = new List<FacetData4D>();
        unionMeshes.AddRange(_AoutB);
        unionMeshes.AddRange(_BoutA);
        SaveResultsToJSON(unionMeshes,"Union");
    }

    //Calculate the intersection
    [ContextMenu("Run IntersectionBoolean")]
    private void IntersectionBoolean()
    {
        CutMeshes();
        var intersectionMeshes = new List<FacetData4D>();
        intersectionMeshes.AddRange(_AinB);
        intersectionMeshes.AddRange(_BinA);
        SaveResultsToJSON(intersectionMeshes,"Intersection");
    }

    //Calculate the set difference
    [ContextMenu("Run DifferenceBoolean(A cut B)")]
    private void DifferenceBoolean()
    {
        CutMeshes();
        var differenceMeshes = new List<FacetData4D>();

        //Flip Normals
        foreach (var meshOf_AinB in _AinB) differenceMeshes.Add(new FacetData4D(meshOf_AinB.FacetVert, -meshOf_AinB.FacetNormal));
        differenceMeshes.AddRange(_BoutA);
        SaveResultsToJSON(differenceMeshes,"Difference");
    }

    //Cutting facets with each other's facets
    private void CutMeshes()
    {
        INV_CELLSIZE = 1 / CELLSIZE;
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
            MaxMin(data, currentAMax, currentAMin, out Vector4 max, out Vector4 min);
            currentAMax = max;
            currentAMin = min;
        }
        var meshA_AABB = new AABB4D(currentAMax, currentAMin);

        var currentBMax = _dataB[0].FacetVert[0];
        var currentBMin = _dataB[0].FacetVert[0];
        foreach (var data in _dataB)
        {
            MaxMin(data, currentBMax, currentBMin, out Vector4 max, out Vector4 min);
            currentBMax = max;
            currentBMin = min;
        }
        var meshB_AABB = new AABB4D(currentBMax, currentBMin);

        var facetDictionaryB = RegisterFacetToCell(_dataB);//Register which cells each facet of one mesh exists in
        var filteredPairs = new HashSet<(FacetData4D facetA, FacetData4D facetB)>();//Pairs filtered by AABB (HashSet that prevents duplicates)

        //Filter by AABB
        foreach (var facetA in _dataA)
        {
            AABB4D aabbA = BuildAABB(facetA);
            Vector4Int maxCell = GetCellIndex(aabbA.Max);
            Vector4Int mimCell = GetCellIndex(aabbA.Min);
            for (int x = mimCell.x; x <= maxCell.x; x++)
            for (int y = mimCell.y; y <= maxCell.y; y++)
            for (int z = mimCell.z; z <= maxCell.z; z++)
            for (int w = mimCell.w; w <= maxCell.w; w++)
            {
                var cellIndex = new Vector4Int(x, y, z, w);
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
       
        var cuttedFacetsA = new HashSet<FacetData4D>(_dataA);//The final chopped facet of Mesh A (Facet A)
        var cuttedFacetsB = new HashSet<FacetData4D>(_dataB);//The final chopped facet of Mesh B (Facet B)
        var cuttedFacetsDictonaryA = new Dictionary<FacetData4D, List<FacetData4D>>();//A dictionary of facet B that slices facet A
        var cuttedFacetsDictonaryB = new Dictionary<FacetData4D, List<FacetData4D>>();//Dictionary of facet A chopping facet B
        var crossedFacetsA = new List<FacetData4D>();//Facet A actually intersects with facet B
        var crossedFacetsB = new List<FacetData4D>();//Facet B actually intersects with facet A

        //Actual intersection or filtering
        foreach (var pair in filteredPairs)
        {
            FacetData4D facetA = pair.facetA;
            FacetData4D facetB = pair.facetB;

            //Do they actually intersect
            if (IsFacetCrossing(facetA,facetB,out List<Vector4> intersects))
            {
                //If facetA is not registered, create a new key
                if (!cuttedFacetsDictonaryA.ContainsKey(facetA))
                {
                    cuttedFacetsDictonaryA[facetA] = new List<FacetData4D>();
                    cuttedFacetsA.Remove(facetA);//It will be chopped up so delete it now (I'll add the chopped up version later)
                    crossedFacetsA.Add(facetA); 
                }

                //If facetB is not registered, create a new key.
                if (!cuttedFacetsDictonaryB.ContainsKey(facetB))
                {
                    cuttedFacetsDictonaryB[facetB] = new List<FacetData4D>();
                    cuttedFacetsB.Remove(facetB);//It will be chopped up so delete it now (I'll add the chopped up version later)
                    crossedFacetsB.Add(facetB);
                }
                cuttedFacetsDictonaryA[facetA].Add(facetB);//Register facetB as the face to cut facetA
                cuttedFacetsDictonaryB[facetB].Add(facetA);//Register facetA as the face to cut facetB
            }
        }

        //Slice off facets that are actually found to intersect
        foreach (var facetA in crossedFacetsA) ArrengeMesh(facetA,cuttedFacetsDictonaryA,cuttedFacetsA);
        foreach (var facetB in crossedFacetsB) ArrengeMesh(facetB,cuttedFacetsDictonaryB,cuttedFacetsB);

        //Registering the cut facets to the spatial cells for inside/outside determination
        var cuttedFacetDictionaryA = RegisterFacetToCell(_dataA);
        var cuttedFacetDictionaryB = RegisterFacetToCell(_dataB);

        //Divide facets into internal and external
        GroupByContainment(cuttedFacetsA,cuttedFacetDictionaryB,meshB_AABB,_AinB,_AoutB);
        GroupByContainment(cuttedFacetsB,cuttedFacetDictionaryA,meshA_AABB,_BinA,_BoutA);
    }
    private List<Vector4> _sideP = new ();//A list of the vertices of the facet that are on one side of the cross section
    private List<Vector4> _sideN = new ();//A list of vertices on the side opposite side P
    private List<Vector4> _onPlane = new ();//Points on the cross section
    private List<FacetData4D> _currentMeshList = new ();//Facets being cut into pieces
    private List<FacetData4D> _nextMeshList = new ();//A list that temporarily stores facets immediately after cutting. Adds them to _currentMeshList each time the loop runs
    private void ArrengeMesh(FacetData4D mesh, Dictionary<FacetData4D, List<FacetData4D>> cuttedMeshesDictonary, HashSet<FacetData4D> cuttedMeshes)//Cutting the mesh
    {
        var cuttingPlanes = cuttedMeshesDictonary[mesh];
        _currentMeshList.Clear();
        _currentMeshList.Add(mesh);

        Vector4 meshNormal = mesh.FacetNormal;
        foreach (var cuttingPlane in cuttingPlanes)
        {
            _nextMeshList.Clear();
            foreach (var currentMesh in _currentMeshList)
            {
                //Since the test is performed on the new cut facet, a test must be performed again.
                if (!IsFacetCrossing(currentMesh, cuttingPlane, out List<Vector4> intersects))
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
                    float dist = Vector4.Dot(vertex - cuttingPlane.FacetVert[0], cuttingPlane.FacetNormal);

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
                if (_onPlane.Count >= 3 || _sideP.Count == 0 || _sideN.Count == 0)
                {
                    _nextMeshList.Add(currentMesh);
                    continue;
                }
                //What to do if the cross section passes through more than one vertex and separates the facets
                if (_onPlane.Count >= 1)
                { 
                    var allPoints = new List<Vector4>();
                    allPoints.AddRange(intersects);
                    allPoints.AddRange(_onPlane);

                    // Again, remove duplicates globally
                    var uniqueKeys = new HashSet<Vector4Int>();
                    var finalIntersects = new List<Vector4>();
                    foreach(var p in allPoints)
                    {
                        var key = QuantizeVector4(p);
                        if (uniqueKeys.Add(key))
                        {
                            finalIntersects.Add(p);
                        }
                    }
                    intersects = finalIntersects;
                }

                //Divide tetrahedrons (trapezoids into multiple tetrahedrons)
                TessellateFacet(_sideP, intersects, meshNormal, _nextMeshList);
                TessellateFacet(_sideN, intersects, meshNormal, _nextMeshList);
            }
            var tempList = _currentMeshList;
            _currentMeshList = _nextMeshList;
            _nextMeshList = tempList;//Add the split facet to the next loop
        }

        foreach(var devidedMesh in _currentMeshList) cuttedMeshes.Add(devidedMesh);
    }
    
    //Grouping split facets
    private void GroupByContainment(HashSet<FacetData4D> cuttedMeshes, Dictionary<Vector4Int, List<(FacetData4D, AABB4D)>> meshDictionary,AABB4D aabb, HashSet<FacetData4D> XinY, HashSet<FacetData4D> XoutY)
    {
        //Intersection event, coordinates rounded to a tolerance and defined as an int key
        var crossEvents = new Dictionary<int, List<float>>();

        foreach (var cuttedMesh in cuttedMeshes)
        {
            crossEvents.Clear();
            var center = cuttedMesh.FacetCenter;
            var end = new Vector4(aabb.Max.x + 1.0f, center.y, center.z, center.w); //Increase the length of the lei by 1 for insurance purposes.
            var startCell = GetCellIndex(center);
            var endCell = GetCellIndex(end);

            //Check from the start cell to the end cell
            for (int x = startCell.x; x <= endCell.x; x++)
            {
                var currentCell = new Vector4Int(x, startCell.y, startCell.z, startCell.w);
                //Find the facets belonging to the current cell from the current cell
                if (meshDictionary.TryGetValue(currentCell, out var meshes))
                {
                    foreach (var mesh in meshes)
                    {
                        //If the ray crosses, register for the crossing event.
                        if (IsRayCrossingFacet(center, end, mesh.Item1, out Vector4 intersection))
                        {
                            var key = Mathf.RoundToInt(intersection.x * INV_EPS);

                            float normalX = mesh.Item1.FacetNormal.x;
                            //Very close intersections are treated as one event
                            if (!crossEvents.ContainsKey(key))
                            {
                                crossEvents[key] = new List<float>();
                            }
                            crossEvents[key].Add(normalX);
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
                    foreach (var normalX in hits)
                    {
                        if (normalX >= 0) hasPositive = true;
                        if (normalX < 0) hasNegative = true;
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
            }
            else
            {
                XinY.Add(cuttedMesh);
            }
        }
    }
    
    
    private AABB4D BuildAABB(FacetData4D facet)
    {
        MaxMin(facet,facet.FacetVert[0],facet.FacetVert[0], out Vector4 max, out Vector4 min);
        return new AABB4D(max, min);
    }
    private void MaxMin(FacetData4D facet,Vector4 defaultMax,Vector4 defaultMin, out Vector4 max, out Vector4 min)
    {
        Vector4 Max = defaultMax;
        Vector4 Min = defaultMin;
        for (int j = 0; j < facet.FacetVert.Length; j++)
        {
            Max = Vector4.Max(Max, facet.FacetVert[j]);
            Min = Vector4.Min(Min, facet.FacetVert[j]);
        }
        max = Max;
        min = Min;
    }
    private bool AABBCheck(AABB4D a, AABB4D b)
    {
        // Checking for overlaps on each axis
        bool overlapX = a.Min.x <= b.Max.x && a.Max.x >= b.Min.x;
        bool overlapY = a.Min.y <= b.Max.y && a.Max.y >= b.Min.y;
        bool overlapZ = a.Min.z <= b.Max.z && a.Max.z >= b.Min.z;
        bool overlapW = a.Min.w <= b.Max.w && a.Max.w >= b.Min.w;

        // If they do not overlap on all four axes, they do not intersect.
        if (!overlapX || !overlapY || !overlapZ || !overlapW) return false;
        return true;
    }

    //Registering facets to spatial cells
    private Dictionary<Vector4Int,List<(FacetData4D,AABB4D)>> RegisterFacetToCell(FacetData4D[] facets)
    {
        // Count the number of facets in each cell
        var cellCounts = new Dictionary<Vector4Int, int>();
        foreach (var facet in facets)
        {
            var aabb = BuildAABB(facet);
            Vector4Int maxCell = GetCellIndex(aabb.Max);
            Vector4Int minCell = GetCellIndex(aabb.Min);
            for (int x = minCell.x; x <= maxCell.x; x++)
            for (int y = minCell.y; y <= maxCell.y; y++)
            for (int z = minCell.z; z <= maxCell.z; z++)
            for (int w = minCell.w; w <= maxCell.w; w++)
            {
                var cellIndex = new Vector4Int(x, y, z, w);
                if (!cellCounts.ContainsKey(cellIndex))
                {
                    cellCounts[cellIndex] = 0;
                }
                cellCounts[cellIndex]++;
            }
        }

        //Build dictionaries and lists with the correct capacity (to prevent GC)
        var facetDictionary = new Dictionary<Vector4Int, List<(FacetData4D, AABB4D)>>(cellCounts.Count);
        // Create a list with the correct capacity in advance based on the dictionary keys.
        foreach (var pair in cellCounts)
        {
            facetDictionary.Add(pair.Key, new List<(FacetData4D, AABB4D)>(pair.Value));
        }

        //Storing Facets
        foreach (var facet in facets)
        {
            var aabb = BuildAABB(facet);
            Vector4Int maxCell = GetCellIndex(aabb.Max);
            Vector4Int mimCell = GetCellIndex(aabb.Min);
            for (int x = mimCell.x; x <= maxCell.x; x++)
            for (int y = mimCell.y; y <= maxCell.y; y++)
            for (int z = mimCell.z; z <= maxCell.z; z++)
            for (int w = mimCell.w; w <= maxCell.w; w++)
            {
                var cellIndex = new Vector4Int(x, y, z, w);
                facetDictionary[cellIndex].Add((facet, aabb));
            }
        }
        return facetDictionary;
    }

    List<Vector4> _targetIntercects = new ();
    List<Vector4> _cutterIintercects = new ();
    //Determine if facets actually intersect
    private bool IsFacetCrossing(FacetData4D targetMesh,FacetData4D cutterMesh,out List<Vector4> intersects)
    {
        _targetIntercects.Clear();
        _cutterIintercects.Clear();
        CulculateEdgePoint(targetMesh, cutterMesh.FacetNormal, cutterMesh.FacetVert[0], _targetIntercects);
        CulculateEdgePoint(cutterMesh, targetMesh.FacetNormal, targetMesh.FacetVert[0], _cutterIintercects);
        intersects = _targetIntercects; 
        if (_targetIntercects.Count < 3 || _cutterIintercects.Count < 3) return false;
        Vector4 origin = _targetIntercects[0];
        Vector4 u = (_targetIntercects[1] - origin).normalized;
        Vector4 v = Vector4.zero;

        //------------------------------
        //SAT Algorithm
        //------------------------------
        for (int i = 2; i < _targetIntercects.Count; i++)
        {
            Vector4 tempVec = _targetIntercects[i] - origin;
            v = (tempVec - Vector4.Dot(tempVec, u) * u).normalized;
            if (v.sqrMagnitude > EPS) break;
        }
        if (v.sqrMagnitude < EPS) return false;
        List<Vector2> polyA_2D = new List<Vector2>(_targetIntercects.Count);
        foreach (var p4d in _targetIntercects)
        {
            Vector4 rel = p4d - origin;
            polyA_2D.Add(new Vector2(Vector4.Dot(rel, u), Vector4.Dot(rel, v)));
        }
        List<Vector2> polyB_2D = new List<Vector2>(_cutterIintercects.Count);
        foreach (var p4d in _cutterIintercects)
        {
            Vector4 rel = p4d - origin;
            polyB_2D.Add(new Vector2(Vector4.Dot(rel, u), Vector4.Dot(rel, v)));
        }
        if (DoPolygonsIntersect_SAT(polyA_2D, polyB_2D))
        {
            return true;
        }
        return false;
    }
    private bool DoPolygonsIntersect_SAT(List<Vector2> polyA, List<Vector2> polyB)
    {
        if (IsSeparatedOnAxes(polyA, polyB)) return false;
        if (IsSeparatedOnAxes(polyB, polyA)) return false;
        return true;
    }
    private bool IsSeparatedOnAxes(List<Vector2> poly1, List<Vector2> poly2)
    {
        for (int i = 0; i < poly1.Count; i++)
        {
            Vector2 p1 = poly1[i];
            Vector2 p2 = poly1[(i + 1) % poly1.Count];
        
            Vector2 edge = p2 - p1;
            Vector2 axis = new Vector2(-edge.y, edge.x); 

            ProjectPolygonOntoAxis(axis, poly1, out float min1, out float max1);
            ProjectPolygonOntoAxis(axis, poly2, out float min2, out float max2);
            if (max1 < min2 || max2 < min1)
            {
                return true;
            }
        }
        return false;
    }
    private void ProjectPolygonOntoAxis(Vector2 axis, List<Vector2> polygon, out float min, out float max)
    {
        if (polygon.Count == 0)
        {
            min = 0;
            max = 0;
            return;
        }
        min = Vector2.Dot(polygon[0], axis);
        max = min;
        for (int i = 1; i < polygon.Count; i++)
        {
            float p = Vector2.Dot(polygon[i], axis);
            if (p < min) min = p;
            else if (p > max) max = p;
        }
    }

    List<Vector4> _intersects = new ();
    HashSet<Vector4Int> _uniqueKeys = new ();
    //Finding the intersection of a facet and an infinite plane
    private void CulculateEdgePoint(FacetData4D mesh,Vector4 planeNormal, Vector4 planePoint, List<Vector4> uniqueIntersects)
    {
        _intersects.Clear();
        Vector4 p0 = mesh.FacetVert[0];
        Vector4 p1 = mesh.FacetVert[1];
        Vector4 p2 = mesh.FacetVert[2];
        Vector4 p3 = mesh.FacetVert[3];
        if (TryCulculateIntercention(p0, p1, planeNormal, planePoint, out Vector4 intersection0)) _intersects.Add(intersection0);
        if (TryCulculateIntercention(p0, p2, planeNormal, planePoint, out Vector4 intersection1)) _intersects.Add(intersection1);
        if (TryCulculateIntercention(p0, p3, planeNormal, planePoint, out Vector4 intersection2)) _intersects.Add(intersection2);
        if (TryCulculateIntercention(p1, p2, planeNormal, planePoint, out Vector4 intersection3)) _intersects.Add(intersection3);
        if (TryCulculateIntercention(p1, p3, planeNormal, planePoint, out Vector4 intersection4)) _intersects.Add(intersection4);
        if (TryCulculateIntercention(p2, p3, planeNormal, planePoint, out Vector4 intersection5)) _intersects.Add(intersection5);
        if (_intersects.Count > 0)
        {
            _uniqueKeys.Clear();
            foreach (var p in _intersects)
            {
                var key = QuantizeVector4(p);
                if (_uniqueKeys.Add(key)) uniqueIntersects.Add(p); // Add original coordinates only if key is unique
            }
        }
    }

    //Finding the intersection of an infinite plane and a line
    private bool TryCulculateIntercention(Vector4 start,Vector4 end, Vector4 planeNormal, Vector4 planePoint,out Vector4 intersection)
    { 
        intersection = Vector4.zero;
        Vector4 d = end - start;
        float denominator = Vector4.Dot(planeNormal, d);

        if (Mathf.Abs(denominator) < EPS) return false;
        float t = Vector4.Dot(planeNormal, planePoint - start) / denominator;

        if (t < 0f || t > 1f) return false; // Exclude outside the line
        intersection = start + t * d;
        return true;
    }

    List<Vector4> _inputVertices = new ();
    //Cut the facet with an infinite plane and divide it into simplex parts
    private void TessellateFacet(List<Vector4> simplexVerts, List<Vector4> intersects, Vector4 meshNormal, List<FacetData4D> nextMeshList)
    {
        _inputVertices.Clear();
        if (simplexVerts.Count == 1 && intersects.Count == 3)
        { 
            nextMeshList.Add(new FacetData4D(new Vector4[] { simplexVerts[0], intersects[0], intersects[1], intersects[2] }, meshNormal));
            return;
        } 
        _inputVertices.Add(intersects[0]);
        _inputVertices.Add(intersects[1]);
        _inputVertices.Add(intersects[2]);
        _inputVertices.Add(simplexVerts[0]);
        if(intersects.Count==4) _inputVertices.Add(intersects[3]);
        for (int i = 1; i < simplexVerts.Count; i++) _inputVertices.Add(simplexVerts[i]);

        GenerateConvex(_inputVertices.ToArray(),meshNormal, nextMeshList);
    }

    private bool IsRayCrossingFacet(Vector4 rayStart, Vector4 rayEnd, FacetData4D facet, out Vector4 intersection)
    {
        intersection = Vector4.zero;
        //Find the intersection of a ray with an infinite plane parallel to the facet
        if (!TryCulculateIntercention(rayStart, rayEnd, facet.FacetNormal, facet.FacetVert[0], out Vector4 p)) return false;
        
        //Determine whether an intersection is inside or outside using varicentric coordinates
        Vector4 v0 = facet.FacetVert[1] - facet.FacetVert[0];
        Vector4 v1 = facet.FacetVert[2] - facet.FacetVert[0];
        Vector4 v2 = facet.FacetVert[3] - facet.FacetVert[0];
        Vector4 v3 = p - facet.FacetVert[0];

        float dot00 = Vector4.Dot(v0, v0);
        float dot01 = Vector4.Dot(v0, v1);
        float dot02 = Vector4.Dot(v0, v2);
        float dot11 = Vector4.Dot(v1, v1);
        float dot12 = Vector4.Dot(v1, v2);
        float dot22 = Vector4.Dot(v2, v2);
        
        float dot30 = Vector4.Dot(v3, v0);
        float dot31 = Vector4.Dot(v3, v1);
        float dot32 = Vector4.Dot(v3, v2);
        // Calculate the denominator of a determinant
        float det = dot00 * (dot11 * dot22 - dot12 * dot12) -
                    dot01 * (dot01 * dot22 - dot12 * dot02) +
                    dot02 * (dot01 * dot12 - dot11 * dot02);

        // If det is very small, it is considered as no intersection.
        if (Mathf.Abs(det) < EPS) return false;
        float invDenom = 1.0f / det;
        // caluculate u
        float detU = dot30 * (dot11 * dot22 - dot12 * dot12) -
                     dot31 * (dot01 * dot22 - dot02 * dot12) +
                     dot32 * (dot01 * dot12 - dot02 * dot11);
        float u = detU * invDenom;
        // calculate v
        float detV = dot00 * (dot31 * dot22 - dot32 * dot12) -
                     dot01 * (dot30 * dot22 - dot32 * dot02) +
                     dot02 * (dot30 * dot12 - dot31 * dot02);
        float v = detV * invDenom;
        // caluculate w
        float detW = dot00 * (dot11 * dot32 - dot12 * dot31) -
                     dot01 * (dot01 * dot32 - dot12 * dot30) +
                     dot02 * (dot01 * dot31 - dot11 * dot30);
        float w = detW * invDenom;

        intersection = p;

        return (u >= -EPS) && (v >= -EPS) && (w >= -EPS) && (u + v + w <= 1.0f+EPS);
    }

    //Get spatial cell index from coordinates
    private Vector4Int GetCellIndex(Vector4 P)
    {
        int x = (int)Math.Floor(P.x * INV_CELLSIZE);
        int y = (int)Math.Floor(P.y * INV_CELLSIZE);
        int z = (int)Math.Floor(P.z * INV_CELLSIZE);
        int w = (int)Math.Floor(P.w * INV_CELLSIZE);

        return new Vector4Int(x, y, z, w);
    }
    private Vector4Int QuantizeVector4(Vector4 vertex)
    {
        // Scale the coordinates by the inverse error (INV_EPS) and round to integers
        return new Vector4Int(
            Mathf.RoundToInt(vertex.x * INV_EPS),
            Mathf.RoundToInt(vertex.y * INV_EPS),
            Mathf.RoundToInt(vertex.z * INV_EPS),
            Mathf.RoundToInt(vertex.w * INV_EPS)
        );
    }



   　//------------------------------
    //Direct Quickhull Algorithm
    //------------------------------
    private Vector4[] _vertices; // Input vertices
    private Vector4 _baseNormal;//The normal of the facet being split
    private Dictionary<BooleanFacetIdx4D, BooleanFacetInfo4D> _activeFacets = new();//The set of currently surfaced facets that should be processed next
    private HashSet<int> _remainingPointIndices = new();//remaining Points
    private List<BooleanSimplex4D> _simplexList = new();//Stores the simplex generated as it grows
    private Vector4 _initialCentroid;//Center of gravity of the initial simplex
    public void GenerateConvex(Vector4[] inputVertices, Vector4 inputNormal, List<FacetData4D> nextMeshList)
    {
        //Initialization
        _vertices = inputVertices;
        _baseNormal = inputNormal;
        _activeFacets.Clear();
        _remainingPointIndices.Clear();
        _simplexList.Clear();

        BooleanSimplex4D initialSimplex = new(0,1,2,3);
        _simplexList.Add(initialSimplex);
        InitializeActiveFaces(initialSimplex);

        for (int i = 0; i < inputVertices.Length; i++) _remainingPointIndices.Add(i);//Add all indices to the remaining points list
        _remainingPointIndices.Remove(initialSimplex.V0);
        _remainingPointIndices.Remove(initialSimplex.V1);
        _remainingPointIndices.Remove(initialSimplex.V2);
        _remainingPointIndices.Remove(initialSimplex.V3);//Remove points belonging to the first simplex from the remaining points list

        _initialCentroid = (inputVertices[0] + inputVertices[1] + inputVertices[2] + inputVertices[3]) * 0.25f;

        while (_activeFacets.Count > 0)
        {
            BooleanFacetInfo4D currentFacet = _activeFacets.Values.First();//Extracting a facet from a dictionary
            var validPoint = FindValidPoint(currentFacet);
            if (validPoint != -1) {
                _remainingPointIndices.Remove(validPoint); //The vertex has been confirmed, so remove it from the processing vertex list
                ProcessPoint(validPoint);//Now that we have found the exterior point, we expand the convex hull
            } 
            else 
            {
                _activeFacets.Remove(currentFacet.FacetIdx);//It no longer needs to be processed, so it is removed from the set of facets to be processed next
            }
        }
        foreach (var simplex in _simplexList)
        {
            FacetData4D facetData = new(new Vector4[] { _vertices[simplex.V0], _vertices[simplex.V1], _vertices[simplex.V2], _vertices[simplex.V3] },_baseNormal);
            nextMeshList.Add(facetData);
        }
    }

    private Dictionary<BooleanFacetIdx4D, BooleanFacetInfo4D> _visibleFaces = new ();//Facets visible from the new vertex
    HashSet<BooleanRidgeIdx4D> _horizonRidges = new ();//Boundary Ridge
    private BooleanRidgeIdx4D[] _facetRidges = new BooleanRidgeIdx4D[3];//Facet Ridges
    private void InitializeActiveFaces(BooleanSimplex4D simplexIndices)
    {
        //Define all facets and their information from the vertex information of a single unit
        int i0 = simplexIndices.V0, i1 = simplexIndices.V1, i2 = simplexIndices.V2, i3 = simplexIndices.V3;
        _activeFacets.Add(new BooleanFacetIdx4D(i1,i2,i3),CulFaceInfo(new BooleanFacetIdx4D(i1,i2,i3), _vertices[i0]));
        _activeFacets.Add(new BooleanFacetIdx4D(i0,i2,i3),CulFaceInfo(new BooleanFacetIdx4D(i0,i2,i3), _vertices[i1]));
        _activeFacets.Add(new BooleanFacetIdx4D(i0,i1,i3),CulFaceInfo(new BooleanFacetIdx4D(i0,i1,i3), _vertices[i2]));
        _activeFacets.Add(new BooleanFacetIdx4D(i0,i1,i2),CulFaceInfo(new BooleanFacetIdx4D(i0,i1,i2), _vertices[i3]));
    }
    private BooleanFacetInfo4D CulFaceInfo(BooleanFacetIdx4D faceVerts, Vector4 baseCenter)
    { 
        //Calculate facet information from facets belonging to a simplex and their backside points
        Vector4 v0 = _vertices[faceVerts.V0];
        Vector4 v1 = _vertices[faceVerts.V1];
        Vector4 v2 = _vertices[faceVerts.V2];
        Vector4 center = (v0 + v1 + v2) * 0.3333333f;
        Vector4 normal = GA4D.Cross4D(v1 - v0, v2 - v0, _baseNormal);
        if (Vector4.Dot(normal, center - baseCenter) < EPS) normal = -normal;
        return new BooleanFacetInfo4D(faceVerts, normal, center);
    }
    private int FindValidPoint(BooleanFacetInfo4D face)
    {
        int validPointIdx = -1;//Set the impossible index first
        foreach (int remainingPoint in _remainingPointIndices)//Run it for the number of vertices
        {
            var dir = _vertices[remainingPoint] - face.FacetCenter;
            if (Vector4.Dot(dir, face.FacetNormal) > EPS) validPointIdx = remainingPoint;
        }
        return validPointIdx;
    }
    private void ProcessPoint(int newPointIndex)
    {
        var visibleFaces = FindVisibleFaces(newPointIndex);//Identify the facets visible from the new vertex
        BuildNewActiveFaces(newPointIndex,visibleFaces);//Create a simplex with the new vertex and visible facets and update activeFacets
    }
    private Dictionary<BooleanFacetIdx4D, BooleanFacetInfo4D> FindVisibleFaces(int newPointIndex)
    {
        _visibleFaces.Clear();
        Vector4 newPoint = _vertices[newPointIndex];

        foreach (var face in _activeFacets.Values)
        {
            Vector4 fromFaceToPoint = face.FacetCenter - newPoint;
            //If the dot product of the vector from the new point to the centroid of a facet and the normal to that facet is negative, then it is visible
            if (Vector4.Dot(face.FacetNormal, fromFaceToPoint) < -EPS)
            {
                _visibleFaces.Add(face.FacetIdx, face);//Add Visible Facets
            }
        }
        return _visibleFaces;
    }
    private void BuildNewActiveFaces(int newPointIndex,Dictionary<BooleanFacetIdx4D, BooleanFacetInfo4D> visibleFacets)
    {
        foreach (var facet in visibleFacets.Values)
        {
            //Create a simplex that connects the visible facets to the new vertices
            BooleanFacetIdx4D BooleanFacetIdx4D = facet.FacetIdx;
            _simplexList.Add(new BooleanSimplex4D(newPointIndex, BooleanFacetIdx4D.V0, BooleanFacetIdx4D.V1, BooleanFacetIdx4D.V2));
        }
        var horizonRidges = FindHorizonRidges(visibleFacets);//Identifying ridges at visible facet boundaries
        foreach (var ridge in horizonRidges)
        {
            BooleanFacetIdx4D BooleanFacetIdx4D = new(ridge.V0, ridge.V1, newPointIndex);
            var facet = CulFaceInfo(BooleanFacetIdx4D, _initialCentroid);//Build the outer facet by connecting the boundary ridge and the new point
            _activeFacets[facet.FacetIdx] = facet;
        }
        foreach (var key in visibleFacets.Keys)
        { 
            _activeFacets.Remove(key);//All visible facets were made into individual units and were no longer visible, so they were deleted
        }
    }
    private HashSet<BooleanRidgeIdx4D> FindHorizonRidges(Dictionary<BooleanFacetIdx4D, BooleanFacetInfo4D> visibleFacets)
    {
        _horizonRidges.Clear();
        foreach (var facet in visibleFacets.Values)//Loop through visible facets
        {
            _facetRidges[0] = new BooleanRidgeIdx4D(facet.FacetIdx.V0, facet.FacetIdx.V1);
            _facetRidges[1] = new BooleanRidgeIdx4D(facet.FacetIdx.V0, facet.FacetIdx.V2);
            _facetRidges[2] = new BooleanRidgeIdx4D(facet.FacetIdx.V1, facet.FacetIdx.V2);

            foreach (var ridge in _facetRidges)//Extract only those that appear only once
            {
                if (!_horizonRidges.Add(ridge))
                {
                    _horizonRidges.Remove(ridge);
                }
            }
        }
        return _horizonRidges;
    }



    private void SaveResultsToJSON(List<FacetData4D> mixedMeshes, string operationName)
    {
       Debug.Log($"Saving boolean results for {operationName} to JSON file...");
        if (mixedMeshes.Count > 0)
        {
            //Vertex deduplication and indexing logic
            MeshData4D _dataA = ConvertToMeshData(mixedMeshes);

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
    private MeshData4D ConvertToMeshData(List<FacetData4D> cuttedMeshes)
    {
        var vertexIndexLookup = new Dictionary<Vector4Int, int>();
        var finalVertices = new List<Vector4>();
        int nextVertexIndex = 0;

        //Merging vertices and setting indices
        foreach (var triangle in cuttedMeshes)
        {
            foreach (var currentVertex in triangle.FacetVert)
            {
                // Quantize vertices to Vector3Int
                Vector4Int key = QuantizeVector4(currentVertex); 
                if (!vertexIndexLookup.ContainsKey(key))
                {
                    finalVertices.Add(currentVertex); 
                    vertexIndexLookup.Add(key, nextVertexIndex);
                    nextVertexIndex++;
                }
            }
        }

        //Create a face index by the index associated with the vertex
        var finalFacets = new List<FacetInfo4D>();
    
        foreach (var facet in cuttedMeshes)
        {
            int[] facetIndices = new int[4];
            for (int i = 0; i < facetIndices.Length; i++)
            {
                Vector4 currentVertex = facet.FacetVert[i];
                Vector4Int key = QuantizeVector4(currentVertex);
            
                // Enter a vertex in the dictionary and get the corresponding index
                if (vertexIndexLookup.TryGetValue(key, out int foundIndex))
                {
                    facetIndices[i] = foundIndex;
                }
            }
            var newFace = new FacetInfo4D(new FacetIdx4D(facetIndices[0], facetIndices[1], facetIndices[2], facetIndices[3]), facet.FacetNormal, facet.FacetCenter);
            finalFacets.Add(newFace);
        }

        // Creating the final mesh data
        MeshData4D meshData = ScriptableObject.CreateInstance<MeshData4D>();
        meshData.Vertices = finalVertices.ToArray();
        meshData.SurfaceFaces = finalFacets.ToArray();

        return meshData;
    }
    #region Benchmark Tools
    private const int NUMBER_OF_EXECUTIONS = 1;
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
        //UnityEngine.Debug.Log($"Standard Deviation (ばらつき): {stdDev:F4} ms");
        UnityEngine.Debug.Log("------------------------------------");
    }
    #endregion
}