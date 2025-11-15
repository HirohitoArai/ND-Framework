using Geometry4D;
using GeometricAlgebra4D;
using System;
using System.Collections.Generic;
using UnityEngine;

public class RenderMesh4D : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private Material _material;
    [SerializeField] private MeshData4D _meshData;
    [SerializeField] private Transform4D _characterTransform;
    [SerializeField] private float _sliceW;
    public Vector4[] WorldVertices;//Vertices in the world coordinate system. Can be rewritten externally.
    public int TotalVertexCount { get; private set; }//Number of vertex arrays with duplicates
    public int[] TotalIndices { get; private set; }//Vertex array index with duplicates
    private Vector4[] _faceNormal;
    private Vector4[] _orderedVertices;//Array containing extracted vertex information from mesh data
    private Vector4[] _transformedVertices;//Rotated and translated normal vector _orderedVertices
    private Vector4[] _transformedNormal;//Rotated and translated normal vector
    private List<Vector3> _crossPointList = new();//Intersection of an edge and a hyperplane
    private List<Vector3> _renderVertices = new();//Vertices aligned for drawing

    private List<int> _renderIndices = new();//Drawing Index

    private static GA4D _charRotation_rev = new();
    private float EPS = 1e-6f;

    private static readonly int[][] _edgeIndices = {//The combination of vertices that make up a simple edge
        new[] {0,1}, new[] {0,2}, new[] {0,3},
        new[] {1,2}, new[] {1,3}, new[] {2,3}
    };

    private Mesh _mesh;
    private Transform4D _objectTransform;

    void Awake()
    {
        _mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = _mesh;
        GetComponent<MeshRenderer>().material = _material;
        _objectTransform = GetComponent<Transform4D>();
        if (_meshData != null) InitializeData();

        var totalIndices = new List<int>();
        for (int i = 0; i < TotalVertexCount; i++) totalIndices.Add(i);
        TotalIndices = totalIndices.ToArray();

        ApplyTransform();
        WorldtoLocal();
        CutMesh();
    }
    
    private void InitializeData()
    {
        TotalVertexCount = _meshData.SurfaceFaces.Length * 4;
        _faceNormal = new Vector4[_meshData.SurfaceFaces.Length];
        _orderedVertices = new Vector4[TotalVertexCount];
        WorldVertices = new Vector4[TotalVertexCount];

        _transformedVertices = new Vector4[TotalVertexCount];
        _transformedNormal = new Vector4[_meshData.SurfaceFaces.Length];
        //Expand each facet's vertices into a sequential array
        for (int i = 0; i < _meshData.SurfaceFaces.Length; i++)
        {
            _faceNormal[i] = _meshData.SurfaceFaces[i].FacetNormal;
            _orderedVertices[i * 4] = _meshData.Vertices[_meshData.SurfaceFaces[i].FacetIdx.V0];
            _orderedVertices[i * 4 + 1] = _meshData.Vertices[_meshData.SurfaceFaces[i].FacetIdx.V1];
            _orderedVertices[i * 4 + 2] = _meshData.Vertices[_meshData.SurfaceFaces[i].FacetIdx.V2];
            _orderedVertices[i * 4 + 3] = _meshData.Vertices[_meshData.SurfaceFaces[i].FacetIdx.V3];
        }
    }

    //Calculate rotation, scale, and translation. Control the object's orientation.
    private void ApplyTransform()
    {
        if (_transformedVertices == null) return; // Confirm the vertex list is initialized
        GA4D rotation = _objectTransform.GeometricRotation;
        Vector4 position = _objectTransform.Position;
        Vector4 scale = _objectTransform.Scale;

        for (int i = 0; i < _orderedVertices.Length; i++)
        {
            Vector4 v = _orderedVertices[i];
            v.x *= scale.x; v.y *= scale.y; v.z *= scale.z; v.w *= scale.w;
            GA4D.RotateVector(v, rotation, out Vector4 rotated);
            WorldVertices[i] = rotated + position;
        }

        for (int j = 0; j < _faceNormal.Length; j++)
        {
            GA4D.RotateVector(_faceNormal[j], rotation, out Vector4 translatedNormal);//Reverse the character's direction
            _charRotation_rev.Set(_characterTransform.GeometricRotation).Reverse();
            GA4D.RotateVector(translatedNormal, _charRotation_rev, out Vector4 rotatedNormal);//Apply own rotation
            _transformedNormal[j] = rotatedNormal;
        }
    }

    //Transform the object's world coordinates into the camera coordinate system
    private void WorldtoLocal()
    {
        var characterPos = _characterTransform.Position;
        
        //Get the camera basis vectors
        Vector4 forward = _characterTransform.Forward;
        Vector4 right = _characterTransform.Right;
        Vector4 up = _characterTransform.Up;
        Vector4 ana = _characterTransform.Ana;
        for (int i = 0; i < WorldVertices.Length; i++)
        { 
            //Calculate the relative vector from the camera to the object
            Vector4 CtoO = WorldVertices[i] - characterPos;

            //Project the relative vector into each basis
            _transformedVertices[i].z = Vector4.Dot(forward, CtoO);
            _transformedVertices[i].x = Vector4.Dot(right, CtoO);
            _transformedVertices[i].y = Vector4.Dot(up, CtoO);
            _transformedVertices[i].w=Vector4.Dot(ana,CtoO);
        }
    }
    private void CutMesh()
    {
        _renderVertices.Clear();
        for (int g = 0; g < _meshData.SurfaceFaces.Length; g++)
        {
            bool above = false;
            bool below = false;
            for (int i = 0; i < 4; i++)
            {
                float w = _transformedVertices[4 * g + i].w;//The w coordinate of the ith vertex of the gth face
                if (w > _sliceW + EPS) above = true;
                if (w < _sliceW - EPS) below = true;
            }

            //Execute if the vertex of the face is positioned across w
            if (above && below)
            {
                _crossPointList.Clear();
                foreach (var e in _edgeIndices)
                {
                    Vector4 p1 = _transformedVertices[4*g+e[0]];
                    Vector4 p2 = _transformedVertices[4*g+e[1]];

                    float w1_diff = p1.w - _sliceW;
                    float w2_diff = p2.w - _sliceW;

                    // If the edge crosses the hyperplane or the endpoint is on the hyperplane
                    if (w1_diff * w2_diff < EPS)
                    {
                        if (Mathf.Abs(p1.w - p2.w) < EPS) continue;// Cases where the entire edge lies on a hyperplane are excluded
                        Vector3 crossPoint = CalculateIntersection(p1, p2);
                        AddUniquePoint(_crossPointList, crossPoint); //Add without duplication
                    }
                }
                Vector3 projectedNormal = new Vector3(_transformedNormal[g].x, _transformedNormal[g].y, _transformedNormal[g].z);//Normal information of a single face
                if (_crossPointList.Count == 3)
                {
                    AddFaceWithCorrectWinding(_crossPointList[0], _crossPointList[1], _crossPointList[2], projectedNormal);
                }
                else if (_crossPointList.Count == 4)
                {
                    SortPointsForQuad(_crossPointList, projectedNormal);//Sort the order of the rectangles
                    AddFaceWithCorrectWinding(_crossPointList[0], _crossPointList[1], _crossPointList[2], projectedNormal);
                    AddFaceWithCorrectWinding(_crossPointList[0], _crossPointList[2], _crossPointList[3], projectedNormal);
                }
                _crossPointList.Clear();
            }
        }

    }
    private void LateUpdate()
    {
        //Update the drawing only when the state is updated
        bool updated = false;
        if (_characterTransform.IsDirtyforScript)
        {
            ApplyTransform();
            WorldtoLocal();
            updated = true;
        }
        if (updated)
        { 
            CutMesh();
            _renderIndices.Clear();
            for (int i = 0; i < _renderVertices.Count; i++) _renderIndices.Add(i);
            _mesh.Clear();
            _mesh.SetVertices(_renderVertices);
            _mesh.SetTriangles(_renderIndices, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }
    }

    //Intersection of hyperplane and edge on slice_w
    private Vector3 CalculateIntersection(Vector4 p0, Vector4 p1)
    {
        float denominator = p1.w - p0.w;
        float t = (_sliceW - p0.w) / denominator;
        var intersect = p0 + t * (p1 - p0);
        return new Vector3(intersect.x,intersect.y,intersect.z);
    }

    //Check the order of the vertices by the sign of the dot product of the projection vector of the N-dimensional facet normal onto 3D and the normal created from the facet vertices.
    void AddFaceWithCorrectWinding(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 Normal)
    {
        Vector3 tempNormal = Vector3.Cross(p1 - p0, p2 - p0);
        //If the direction of the projected normal and the normal created from the vertex are the same, the order remains the same.
        if (Vector3.Dot(tempNormal, Normal) > 0)
        {
            _renderVertices.Add(p0);
            _renderVertices.Add(p1);
            _renderVertices.Add(p2);
        }
        else//If the projected normal and the normal created from the vertex have opposite directions, swap their order.
        {
            _renderVertices.Add(p0);
            _renderVertices.Add(p2);
            _renderVertices.Add(p1);
        }
    }
    private void AddUniquePoint(List<Vector3> crossPoints, Vector3 newPoint)
    {
        for (int i = 0; i < crossPoints.Count; i++)
        {
            if ((crossPoints[i] - newPoint).sqrMagnitude < EPS * EPS) return;
        }
        crossPoints.Add(newPoint);
    }

    //When a quadrilateral is created after cutting, rearrange the order of the vertices.
    private void SortPointsForQuad(List<Vector3> points, Vector3 normal)
    {
        if (points.Count != 4) return;
        Vector3 center = (points[0] + points[1] + points[2] + points[3]) * 0.25f;
        Vector3 firstVec = points[0] - center;
        for (int i = 1; i < 4; i++)
        {
            Vector3 key = points[i];
            int j = i - 1;
            while (j >= 0 && ComparePoints(key, points[j], center, firstVec, normal))
            {
                points[j + 1] = points[j];
                j--;
            }
            points[j + 1] = key;
        }
    }
    //When four vertices are obtained by cutting, rearrange the vertices in the appropriate order.
    private bool ComparePoints(Vector3 p1, Vector3 p2, Vector3 center, Vector3 firstVec, Vector3 normal)
    {
        Vector3 v1 = p1 - center;// Calculate the relative vector from the reference vector
        Vector3 v2 = p2 - center;

        if ((v1 - firstVec).sqrMagnitude < EPS) return true; // If the reference vector and v1 are in the same direction, v1 comes first.
        if ((v2 - firstVec).sqrMagnitude < EPS) return false;

        // Half plane judgment
        float side1 = Vector3.Dot(normal, Vector3.Cross(firstVec, v1));
        float side2 = Vector3.Dot(normal, Vector3.Cross(firstVec, v2));

        if (side1 >= 0 && side2 < 0) // Check sign to handle zero crossing cases correctly
        {
            return true; // p1 is the first half (0-180 degrees), p2 is the second half (180-360 degrees) -> p1 comes first
        }
        if (side1 < 0 && side2 >= 0)
        {
            return false; // p2 comes first
        }
        float order = Vector3.Dot(normal, Vector3.Cross(v1, v2));
        return order > 0;
    }

    public FacetData4D[] GetBooleanMeshData()//Passing data for boolean operations
    {
        ApplyTransform();
        var resultData = new FacetData4D[_meshData.SurfaceFaces.Length];
        for (int i = 0; i < _meshData.SurfaceFaces.Length; i++)
        {
            GA4D.RotateVector(_meshData.SurfaceFaces[i].FacetNormal, _objectTransform.GeometricRotation, out Vector4 worldCoordinateNormal);
            Vector4[] verts = new Vector4[4];
            for (int j = 0; j < verts.Length; j++)
            {
                verts[j] = WorldVertices[(i*4)+j];
            }
            resultData[i] = new FacetData4D(verts, worldCoordinateNormal);
        }
        return resultData;
    }
}