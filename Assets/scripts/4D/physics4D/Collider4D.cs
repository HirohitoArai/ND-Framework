using GeometricAlgebra4D;
using Geometry4D;
using System.Collections.Generic;
using UnityEngine;

public class Collider4D : MonoBehaviour
{
    public Sphere4D BoundingSphere { get; private set; } // Bounding Sphere for collision detection
    public AABB4D BoundingBox { get; private set; } // AABB for hit detection
    public bool IsPhysicsDriven{ get; private set; }
    public bool IsColliderInitialized { get; private set; } = false;
    public PhysicsBody4D PhysicsBody { get; private set; }
    private RenderMesh4D _renderMesh;
    
    private void Awake()
    {
        PhysicsBody = GetComponent<PhysicsBody4D>();
        _renderMesh = GetComponent<RenderMesh4D>();
        IsPhysicsDriven = (PhysicsBody != null);
        if (PhysicsManager4D.Instance != null) PhysicsManager4D.Instance.RegisterCollider(this);
    }
    void Start()
    {
        if (IsPhysicsDriven)PhysicsBody.InitializePhysicsBody();
        UpdateMaxMin();
        this.IsColliderInitialized = true;
    }
    private void FixedUpdate()
    {
        UpdateMaxMin(); 
    }

    //Calculating the center of gravity and the coordinates required for AABB
    private void UpdateMaxMin()
    {
        Vector4 min = _renderMesh.WorldVertices[0];
        Vector4 max = _renderMesh.WorldVertices[0];
        for (int i = 0; i < _renderMesh.TotalVertexCount; i++)
        {
            min = Vector4.Min(min, _renderMesh.WorldVertices[i]);
            max = Vector4.Max(max, _renderMesh.WorldVertices[i]);
        }
        var center = (max + min) / 2.0f;

        float maxRadiusSq = 0;
        for (int i = 0; i < _renderMesh.TotalVertexCount; i++)
        { 
            float distSq = (_renderMesh.WorldVertices[i] - center).sqrMagnitude; 
            if (distSq > maxRadiusSq)
            {
                maxRadiusSq = distSq;
            }
        }
        BoundingSphere = new Sphere4D(center, Mathf.Sqrt(maxRadiusSq));
        BoundingBox = new AABB4D(max, min);
    }

    //Calculating facet data for objects whose coordinates are updated
    public Tetrahedron[] GetTetrahedronsFromParticles()
    {
        var triList = new List<Tetrahedron>();
        for (int i = 0; i <  _renderMesh.TotalVertexCount; i+=4)
        {
            int p_idx0 = i;
            int p_idx1 = i + 1;
            int p_idx2 = i + 2;
            int p_idx3 = i + 3;

            Vector4 p0 = _renderMesh.WorldVertices[p_idx0];
            Vector4 p1 = _renderMesh.WorldVertices[p_idx1];
            Vector4 p2 = _renderMesh.WorldVertices[p_idx2];
            Vector4 p3 = _renderMesh.WorldVertices[p_idx3];

            Vector4 normal = GA4D.Cross4D(p0 -p1, p0 - p2, p0 -p3).normalized;
            if (Vector4.Dot(normal, p0 - BoundingSphere.Center) < 0) normal = -normal;
            triList.Add(new Tetrahedron(p0,p1,p2,p3,normal,p_idx0, p_idx1, p_idx2,p_idx3));
        }
        return triList.ToArray();
    }
    //Sending a RenderMesh instance to the PhysicsManager
    public RenderMesh4D GetRenderMeshInstance(){return _renderMesh;}
    //Register itself with the PhysicsManager
    void OnDisable(){if (PhysicsManager4D.Instance != null) PhysicsManager4D.Instance.UnregisterCollider(this); }
}
