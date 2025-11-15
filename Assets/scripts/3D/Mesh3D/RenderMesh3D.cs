using GeometricAlgebra3D;
using Geometry3D;
using System.Collections.Generic;
using UnityEngine;

public class RenderMesh3D : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private Material _material;
    [SerializeField] private MeshData3D _meshData;
    [SerializeField] private Transform3D _characterTransform;
    public Vector3[] WorldVertices;//Vertices in the world coordinate system. Can be rewritten externally.
    public int TotalVertexCount { get; private set; }//Number of vertices for drawing
    public int[] TotalIndices { get; private set; }//Drawing Index

    private Vector3[] _orderedVertices;//Initial coordinates with the triangles correctly ordered
    private Vector3[] _renderVertices;//Vertices aligned for drawing
    
    private Mesh _mesh;
    private Transform3D _objectTransform;
    
    void Awake()
    {
        _mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = _mesh;
        GetComponent<MeshRenderer>().material = _material;
        _objectTransform = GetComponent<Transform3D>();
        if (_meshData != null) InitializeData();

        ApplyTransform();
        WorldtoLocal();
        
        var totalIndices = new List<int>();
        for (int i = 0; i < TotalVertexCount; i++) totalIndices.Add(i);
        TotalIndices = totalIndices.ToArray();

        _mesh.SetVertices(WorldVertices);
        _mesh.SetTriangles(TotalIndices,0);
        _mesh.RecalculateNormals();
        _mesh.MarkDynamic();
    }

    private void InitializeData()
    {
        TotalVertexCount = _meshData.SurfaceFaces.Length * 3;
        _orderedVertices = new Vector3[TotalVertexCount];
        _renderVertices = new Vector3[TotalVertexCount];
        WorldVertices = new Vector3[TotalVertexCount];
        int idx = 0;
        for (int i = 0; i < _meshData.SurfaceFaces.Length; i++)
        {
            AddFaceWithCorrectWinding(_meshData.SurfaceFaces[i], ref idx);
        }
    }
    //Calculate rotation, scale, and translation. Control the object's orientation.
    private void ApplyTransform()
    {
        if (WorldVertices == null) return; // Ensure the vertex list is initialized
        GA3D rotation = _objectTransform.GeometricRotation;
        Vector3 position = _objectTransform.Position;
        Vector3 scale = _objectTransform.Scale;

        for (int i = 0; i < TotalVertexCount; i++)
        {
            var v = _orderedVertices[i];
            v.x *= scale.x; v.y *= scale.y; v.z *= scale.z;
            GA3D.RotateVector(v, rotation, out Vector3 rotated);
            WorldVertices[i] = rotated + position;
        }
    }

    //Transform the object's world coordinates into the camera coordinate system
    private void WorldtoLocal()
    {
        Vector3 characterPos = _characterTransform.Position;

        //Get the camera basis vectors
        Vector3 forward = _characterTransform.Forward;
        Vector3 right = _characterTransform.Right;
        Vector3 up = _characterTransform.Up;
        for (int i = 0; i < TotalVertexCount; i++)
        {
            //Calculate the relative vector from the camera to the object
            Vector3 CtoO = WorldVertices[i] - characterPos;

            //Project the relative vector into each basis
            _renderVertices[i].z = Vector3.Dot(forward,CtoO);
            _renderVertices[i].x = Vector3.Dot(right,CtoO);
            _renderVertices[i].y = Vector3.Dot(up,CtoO);
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
            _mesh.SetVertices(_renderVertices);
            _mesh.RecalculateBounds();
        }
    }

    //Check the order of the vertices by the sign of the dot product of the facet normal and the normal vector created from the facet vertices
    private void AddFaceWithCorrectWinding(FacetInfo3D face, ref int idx)
    {
        Vector3 Normal = face.FacetNormal;
        Vector3 p0 = _meshData.Vertices[face.FacetIdx.V0];
        Vector3 p1 = _meshData.Vertices[face.FacetIdx.V1];
        Vector3 p2 = _meshData.Vertices[face.FacetIdx.V2];

        Vector3 tempNormal = Vector3.Cross(p0-p1,p0-p2);

        //If the direction of the projected normal and the normal created from the vertex are the same, the order remains the same.
        if (Vector3.Dot(tempNormal, Normal) > 0)
        {
            _orderedVertices[idx++] = p0;
            _orderedVertices[idx++] = p1;
            _orderedVertices[idx++] = p2;
        }
        else//If the projected normal and the normal created from the vertex have opposite directions, swap their order.
        {
            _orderedVertices[idx++] = p0;
            _orderedVertices[idx++] = p2;
            _orderedVertices[idx++] = p1;
        }
    }

    public FacetData3D[] GetBooleanMeshData()//Passing data for boolean operations
    {
        ApplyTransform();
        var resultData = new FacetData3D[_meshData.SurfaceFaces.Length];
        for (int i = 0; i < _meshData.SurfaceFaces.Length; i++)
        {
            GA3D.RotateVector(_meshData.SurfaceFaces[i].FacetNormal, _objectTransform.GeometricRotation, out Vector3 worldCoordinateNormal);
            Vector3[] verts = new Vector3[3];
            for (int j = 0; j < verts.Length; j++)
            {
                verts[j] = WorldVertices[(i*3)+j];
            }
            resultData[i] = new FacetData3D(verts, worldCoordinateNormal);
        }
        return resultData;
    }
}