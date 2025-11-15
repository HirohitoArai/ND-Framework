using GeometricAlgebra4D;
using Geometry4D;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class BuildConvexMesh4D
{
    private Vector4[] _vertices; // Input vertices
    private Dictionary<FacetIdx4D, FacetInfo4D> _activeFacets = new();//The set of currently surfaced facets that should be processed next
    private HashSet<int> _remainingPointIndices = new();//remaining Points
    private List<FacetInfo4D>_finalizedFacets = new();//The final set of outer facets is stored
    private List<Simplex4D> _simplexList = new ();//Stores the simplex generated as it grows
    private Vector4 _initialCentroid;//Center of gravity of the initial simplex

    private const float EPS = 1e-4f;//Tolerance
    public FacetInfo4D[] GenerateConvex(Vector4[] inputVertices)
    {
        //Initialization
        _vertices = inputVertices;
        _activeFacets.Clear();
        _remainingPointIndices.Clear();
        _finalizedFacets.Clear();
        _simplexList.Clear();

        Simplex4D? initialSimplexNullable = FindInitialSimplexIndex(_simplexList);//Initial simplex generation
        if (initialSimplexNullable == null) {
            Debug.LogError("èâä˙íPëÃÇÃê∂ê¨Ç…é∏îsÅB");
            return null;
        }
        Simplex4D initialSimplex = initialSimplexNullable.Value;
        _simplexList.Add(initialSimplex);
        InitializeActiveFaces(initialSimplex);

        for (int i = 0; i < inputVertices.Length; i++) _remainingPointIndices.Add(i);//Add all indices to the remaining points list
        _remainingPointIndices.Remove(initialSimplex.V0);
        _remainingPointIndices.Remove(initialSimplex.V1);
        _remainingPointIndices.Remove(initialSimplex.V2);
        _remainingPointIndices.Remove(initialSimplex.V3);//Remove points belonging to the first simplex from the remaining points list

        while (_activeFacets.Count > 0)
        {
            FacetInfo4D currentFacet = _activeFacets.Values.First();//Extracting a facet from a dictionary
            int furthestPointIdx = FindFurthestPoint(currentFacet);//Find the point that is vertically farthest from the facet
            if (furthestPointIdx != -1) {
                _remainingPointIndices.Remove(furthestPointIdx); //The vertex has been confirmed, so remove it from the processing vertex list
                ProcessPoint(furthestPointIdx);// Now that we have found the exterior point, we expand the convex hull
            } else {
                _finalizedFacets.Add(new FacetInfo4D(currentFacet.FacetIdx,currentFacet.FacetNormal.normalized,currentFacet.FacetCenter));//Since no external point is found, the current face is determined to be outside the figure.
                _activeFacets.Remove(currentFacet.FacetIdx);//It no longer needs to be processed, so it is removed from the set of facets to be processed next
            }
        }
        return _finalizedFacets.ToArray();//Extract only the information you need from the outer facet list
    }

    private Dictionary<FacetIdx4D, FacetInfo4D> _visibleFaces = new();//Facets visible from the new vertex
    private HashSet<RidgeIdx4D> _horizonRidges = new();//Boundary Ridge
    private RidgeIdx4D[] _facetRidges = new RidgeIdx4D[4];//Facet Ridges

    private Simplex4D? FindInitialSimplexIndex(List<Simplex4D> _simplexList)
    {
        if (_vertices.Length < 5) return null;
        for (int i0 = 0; i0 < _vertices.Length - 4; i0++)
        for (int i1 = i0 + 1; i1 < _vertices.Length - 3; i1++)
        for (int i2 = i1 + 1; i2 < _vertices.Length - 2; i2++)
        for (int i3 = i2 + 1; i3 < _vertices.Length - 1; i3++)
        for (int i4 = i3 + 1; i4 < _vertices.Length; i4++)
        {
            if (HasVolume(_vertices[i0],_vertices[i1],_vertices[i2],_vertices[i3],_vertices[i4]))
            {
                _initialCentroid = (_vertices[i0] + _vertices[i1] + _vertices[i2] + _vertices[i3] + _vertices[i4]) * 0.2f;
                return new Simplex4D(i0,i1,i2,i3,i4);           
            }
        }
        return null;
    }
    private void InitializeActiveFaces(Simplex4D simplexIndices)
    {
        int i0 = simplexIndices.V0, i1 = simplexIndices.V1, i2 = simplexIndices.V2, i3 = simplexIndices.V3, i4=simplexIndices.V4;
        _activeFacets.Add(new FacetIdx4D(i1,i2,i3,i4),CulFaceInfo(new FacetIdx4D(i1,i2,i3,i4), _vertices[i0]));
        _activeFacets.Add(new FacetIdx4D(i0,i2,i3,i4),CulFaceInfo(new FacetIdx4D(i0,i2,i3,i4), _vertices[i1]));
        _activeFacets.Add(new FacetIdx4D(i0,i1,i3,i4),CulFaceInfo(new FacetIdx4D(i0,i1,i3,i4), _vertices[i2]));
        _activeFacets.Add(new FacetIdx4D(i0,i1,i2,i4),CulFaceInfo(new FacetIdx4D(i0,i1,i2,i4), _vertices[i3]));
        _activeFacets.Add(new FacetIdx4D(i0,i1,i2,i3),CulFaceInfo(new FacetIdx4D(i0,i1,i2,i3), _vertices[i4]));
    }
    private FacetInfo4D CulFaceInfo(FacetIdx4D faceVerts, Vector4 baseCenter)
    { 
        //Calculate facet information from facets belonging to a simplex and their backside points
        Vector4 v0 = _vertices[faceVerts.V0];
        Vector4 v1 = _vertices[faceVerts.V1];
        Vector4 v2 = _vertices[faceVerts.V2];
        Vector4 v3 = _vertices[faceVerts.V3];
        Vector4 center = (v0 + v1 + v2 + v3) * 0.25f;
        Vector4 normal = GA4D.Cross4D(v1 - v0, v2 - v0, v3 - v0);
        if (Vector4.Dot(normal, center - baseCenter) < 0) normal = -normal;
        return new FacetInfo4D(faceVerts, normal, center);
    }
    private bool HasVolume(Vector4 a, Vector4 b, Vector4 c, Vector4 d, Vector4 e)//Volume determination
    {
        return GA4D.Volume4D(a-b,a-c,a-d,a-e) > EPS;
    }
    private int FindFurthestPoint(FacetInfo4D face)
    {
        float maxDist = EPS;//First distance
        int furthestPointIdx = -1;//Set the impossible index first

        foreach (int remainingPoint in _remainingPointIndices)//Run it for the number of vertices
        {
            Vector4 dir = _vertices[remainingPoint] - face.FacetCenter;
            float dist = Vector4.Dot(dir, face.FacetNormal);//Get the vertical distance of a facet

            if (dist > maxDist)//If it is farther than the farthest distance recorded so far, it will be updated
            {
                maxDist = dist;
                furthestPointIdx = remainingPoint;
            }
        }
        return furthestPointIdx;
    }
    private void ProcessPoint(int newPointIndex)
    {
        var visibleFaces = FindVisibleFaces(newPointIndex);//Identify the facets visible from the new vertex
        BuildNewActiveFaces(newPointIndex,visibleFaces);//Create a simplex with the new vertex and visible facets and update activeFacets
    }
    private Dictionary<FacetIdx4D, FacetInfo4D> FindVisibleFaces(int newPointIndex)
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
    private void BuildNewActiveFaces(int newPointIndex,Dictionary<FacetIdx4D, FacetInfo4D> visibleFacets)
    {
        foreach (var face in visibleFacets.Values)
        {
            //Create a simplex that connects the visible facets to the new vertices
            FacetIdx4D FacetIdx = face.FacetIdx;
            _simplexList.Add(new Simplex4D(newPointIndex, FacetIdx.V0, FacetIdx.V1, FacetIdx.V2, FacetIdx.V3));
        }
        var horizonEdges = FindHorizonRidges(visibleFacets);//Identifying ridges at visible facet boundaries
        foreach (var edge in horizonEdges)
        {
            FacetIdx4D FacetIdx3D = new(edge.V0, edge.V1,edge.V2, newPointIndex);
            var face = CulFaceInfo(FacetIdx3D, _initialCentroid);//Build the outer facet by connecting the boundary ridge and the new point
            _activeFacets[face.FacetIdx] = face;
        }
        foreach (var key in visibleFacets.Keys)
        { 
            _activeFacets.Remove(key);//All visible facets were made into individual units and were no longer visible, so they were deleted
        }
    }
    private HashSet<RidgeIdx4D> FindHorizonRidges(Dictionary<FacetIdx4D, FacetInfo4D> visibleFaces)
    {
        _horizonRidges.Clear();
        foreach (var facet in visibleFaces.Values)//Loop through visible facets
        {
            _facetRidges[0] = new RidgeIdx4D(facet.FacetIdx.V0, facet.FacetIdx.V1,facet.FacetIdx.V2);
            _facetRidges[1] = new RidgeIdx4D(facet.FacetIdx.V0, facet.FacetIdx.V1,facet.FacetIdx.V3);
            _facetRidges[2] = new RidgeIdx4D(facet.FacetIdx.V0, facet.FacetIdx.V2,facet.FacetIdx.V3);
            _facetRidges[3] = new RidgeIdx4D(facet.FacetIdx.V1, facet.FacetIdx.V2,facet.FacetIdx.V3);

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
}