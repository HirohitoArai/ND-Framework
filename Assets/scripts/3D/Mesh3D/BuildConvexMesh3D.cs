using Geometry3D;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class BuildConvexMesh3D
{
    private Vector3[] _vertices; // Input vertices
    private Dictionary<FacetIdx3D, FacetInfo3D> _activeFacets = new();//The set of currently surfaced facets that should be processed next
    private HashSet<int> _remainingPointIndices = new();//remaining Points
    private List<FacetInfo3D>_finalizedFacets = new();//The final set of outer facets is stored
    private List<Simplex3D> _simplexList = new();//Stores the simplex generated as it grows
    private Vector4 _initialCentroid;//Center of gravity of the initial simplex

    private const float EPS = 1e-4f;//Tolerance
    public FacetInfo3D[] GenerateConvex(Vector3[] inputVertices)
    {
        //Initialization
        _vertices = inputVertices;
        _activeFacets.Clear();
        _remainingPointIndices.Clear();
        _finalizedFacets.Clear();
        _simplexList.Clear();

        Simplex3D? initialSimplexNullable = FindInitialSimplexIndex(_simplexList);//Initial simplex generation
        if (initialSimplexNullable == null) {
            Debug.LogError("èâä˙íPëÃÇÃê∂ê¨Ç…é∏îsÅB");
            return null;
        }
        Simplex3D initialSimplex = initialSimplexNullable.Value;
        _simplexList.Add(initialSimplex);
        InitializeActiveFaces(initialSimplex);

        for (int i = 0; i < inputVertices.Length; i++) _remainingPointIndices.Add(i);//Add all indices to the remaining points list
        _remainingPointIndices.Remove(initialSimplex.V0);
        _remainingPointIndices.Remove(initialSimplex.V1);
        _remainingPointIndices.Remove(initialSimplex.V2);
        _remainingPointIndices.Remove(initialSimplex.V3);//Remove points belonging to the first simplex from the remaining points list

        while (_activeFacets.Count > 0)
        {
            FacetInfo3D currentFacet = _activeFacets.Values.First();//Extracting a facet from a dictionary
            int furthestPointIdx = FindFurthestPoint(currentFacet);//Find the point that is vertically farthest from the facet
            if (furthestPointIdx != -1) {
                _remainingPointIndices.Remove(furthestPointIdx); //The vertex has been confirmed, so remove it from the processing vertex list
                ProcessPoint(furthestPointIdx);// Now that we have found the exterior point, we expand the convex hull
            } else {
                _finalizedFacets.Add(new FacetInfo3D(currentFacet.FacetIdx,currentFacet.FacetNormal.normalized,currentFacet.FacetCenter));//Since no external point is found, the current face is determined to be outside the figure.
                _activeFacets.Remove(currentFacet.FacetIdx);//It no longer needs to be processed, so it is removed from the set of facets to be processed next
            }
        }
        return _finalizedFacets.ToArray();//Extract only the information you need from the outer facet list
    }


    private Dictionary<FacetIdx3D, FacetInfo3D> _visibleFaces = new();//Facets visible from the new vertex
    private HashSet<RidgeIdx3D> _horizonRidges = new();//Boundary Ridge
    private RidgeIdx3D[] _facetRidges = new RidgeIdx3D[3];//Facet Ridges
    private Simplex3D? FindInitialSimplexIndex(List<Simplex3D> _simplexList)
    {
        if (_vertices.Length < 5) return null;
        for (int i0 = 0; i0 < _vertices.Length - 4; i0++)
        for (int i1 = i0 + 1; i1 < _vertices.Length - 3; i1++)
        for (int i2 = i1 + 1; i2 < _vertices.Length - 2; i2++)
        for (int i3 = i2 + 1; i3 < _vertices.Length - 1; i3++)
        {
            if (HasVolume(_vertices[i0], _vertices[i1], _vertices[i2], _vertices[i3]))
            { 
                _initialCentroid = (_vertices[i0] + _vertices[i1] + _vertices[i2] + _vertices[i3]) * 0.25f;
                return new Simplex3D(i0, i1, i2, i3);  
            }
        }
        return null;
    }
    private void InitializeActiveFaces(Simplex3D simplexIndices)
    {
        int i0 = simplexIndices.V0, i1 = simplexIndices.V1, i2 = simplexIndices.V2, i3 = simplexIndices.V3;
        _activeFacets.Add(new FacetIdx3D(i1,i2,i3),CulFaceInfo(new FacetIdx3D(i1,i2,i3), _vertices[i0]));
        _activeFacets.Add(new FacetIdx3D(i0,i2,i3),CulFaceInfo(new FacetIdx3D(i0,i2,i3), _vertices[i1]));
        _activeFacets.Add(new FacetIdx3D(i0,i1,i3),CulFaceInfo(new FacetIdx3D(i0,i1,i3), _vertices[i2]));
        _activeFacets.Add(new FacetIdx3D(i0,i1,i2),CulFaceInfo(new FacetIdx3D(i0,i1,i2), _vertices[i3]));
    }
    public FacetInfo3D CulFaceInfo(FacetIdx3D faceVerts, Vector3 baseCenter)
    { 
        //Calculate facet information from facets belonging to a simplex and their backside points
        Vector3 v0 = _vertices[faceVerts.V0];
        Vector3 v1 = _vertices[faceVerts.V1];
        Vector3 v2 = _vertices[faceVerts.V2];
        Vector3 center = (v0 + v1 + v2) * 0.3333333f;
        Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
        if (Vector3.Dot(normal, center - baseCenter) < -EPS) normal = -normal;
        return new FacetInfo3D(faceVerts, normal, center);
    }
    private bool HasVolume(Vector3 a, Vector3 b, Vector3 c, Vector3 d)//Volume determination
    {
        return Vector3.Dot(Vector3.Cross(a-b,a-c),a-d) > EPS;
    }
    private int FindFurthestPoint(FacetInfo3D face)
    {
        float MaxDist = EPS;//First distance
        int furthestPointIdx = -1;//Set the impossible index first

        foreach (int remainingPoint in _remainingPointIndices)//Run it for the number of vertices
        {
            Vector3 dir = _vertices[remainingPoint] - face.FacetCenter;
            float dist = Vector3.Dot(dir, face.FacetNormal);//Get the vertical distance of a facet

            if (dist > MaxDist)//If it is farther than the farthest distance recorded so far, it will be updated
            {
                MaxDist = dist;
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
    private Dictionary<FacetIdx3D, FacetInfo3D> FindVisibleFaces(int newPointIndex)
    {
        _visibleFaces.Clear();
        Vector3 newPoint = _vertices[newPointIndex];
        foreach (var face in _activeFacets.Values)
        {
            Vector3 fromFaceToPoint = face.FacetCenter - newPoint;
            //If the dot product of the vector from the new point to the centroid of a facet and the normal to that facet is negative, then it is visible
            if (Vector3.Dot(face.FacetNormal, fromFaceToPoint) < -EPS)
            {
                _visibleFaces.Add(face.FacetIdx, face);//Add Visible Facets
            }
        }
        return _visibleFaces;
    }
    private void BuildNewActiveFaces(int newPointIndex,Dictionary<FacetIdx3D, FacetInfo3D> visibleFacets)
    {
        foreach (var facet in visibleFacets.Values)
        {
            //Create a simplex that connects the visible facets to the new vertices
            FacetIdx3D FacetIdx3D = facet.FacetIdx;
            _simplexList.Add(new Simplex3D(newPointIndex, FacetIdx3D.V0, FacetIdx3D.V1, FacetIdx3D.V2));
        }
        var horizonRidges = FindHorizonRidges(visibleFacets);//Identifying ridges at visible facet boundaries
        foreach (var ridge in horizonRidges)
        {
            FacetIdx3D FacetIdx3D = new(ridge.V0, ridge.V1, newPointIndex);
            var facet = CulFaceInfo(FacetIdx3D, _initialCentroid);//Build the outer facet by connecting the boundary ridge and the new point
            _activeFacets[facet.FacetIdx] = facet;
        }
        foreach (var key in visibleFacets.Keys)
        { 
            _activeFacets.Remove(key);//All visible facets were made into individual units and were no longer visible, so they were deleted
        }
    }
    private HashSet<RidgeIdx3D> FindHorizonRidges(Dictionary<FacetIdx3D, FacetInfo3D> visibleFacets)
    {
        _horizonRidges.Clear();
        foreach (var facet in visibleFacets.Values)//Loop through visible facets
        {
        _facetRidges[0] = new RidgeIdx3D(facet.FacetIdx.V0, facet.FacetIdx.V1);
        _facetRidges[1] = new RidgeIdx3D(facet.FacetIdx.V0, facet.FacetIdx.V2);
        _facetRidges[2] = new RidgeIdx3D(facet.FacetIdx.V1, facet.FacetIdx.V2);

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