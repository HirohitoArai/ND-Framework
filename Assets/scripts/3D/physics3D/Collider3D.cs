using Geometry3D;
using System.Collections.Generic;
using UnityEngine;

public class Collider3D : MonoBehaviour
{
    public Sphere3D BoundingSphere { get; private set; } // Bounding Sphere for collision detection
    public AABB3D BoundingBox { get; private set; }// AABB for hit detection
    public bool IsPhysicsDriven{ get; private set; }
    public bool IsColliderInitialized { get; private set; } = false;
    public PhysicsBody3D PhysicsBody { get; private set; }
    private RenderMesh3D _renderMesh;
    
    private void Awake()
    {
        PhysicsBody = GetComponent<PhysicsBody3D>();
        _renderMesh = GetComponent<RenderMesh3D>();
        IsPhysicsDriven = (PhysicsBody != null);
        if (PhysicsManager3D.Instance != null) PhysicsManager3D.Instance.RegisterCollider(this);
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
        Vector3 min = _renderMesh.WorldVertices[0];
        Vector3 max = _renderMesh.WorldVertices[0];
        for (int i = 0; i < _renderMesh.TotalVertexCount; i++)
        {
            min = Vector3.Min(min, _renderMesh.WorldVertices[i]);
            max = Vector3.Max(max, _renderMesh.WorldVertices[i]);
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
        BoundingSphere = new Sphere3D(center, Mathf.Sqrt(maxRadiusSq));
        BoundingBox = new AABB3D(max, min);
    }

    //Calculating facet data for objects whose coordinates are updated
    public Triangle[] GetTrianglesFromParticles()
    {
        var triList = new List<Triangle>();
        for (int i = 0; i < _renderMesh.TotalVertexCount; i += 3)
        {
            int p_idx0 = i;
            int p_idx1 = i + 1;
            int p_idx2 = i + 2;

            Vector3 p0 = _renderMesh.WorldVertices[p_idx0];
            Vector3 p1 = _renderMesh.WorldVertices[p_idx1];
            Vector3 p2 = _renderMesh.WorldVertices[p_idx2];
            Vector3 normal = Vector3.Cross(p0 - p1, p0 - p2).normalized;
            if (Vector3.Dot(normal, p0 - BoundingSphere.Center) < 0) normal = -normal;
            triList.Add(new Triangle(p0, p1, p2, normal, p_idx0, p_idx1, p_idx2));
        }
        return triList.ToArray();
    }
    //Sending a RenderMesh instance to the PhysicsManager
    public RenderMesh3D GetRenderMeshInstance(){return _renderMesh;}
    //Register itself with the PhysicsManager
    void OnDisable(){ if (PhysicsManager3D.Instance != null) PhysicsManager3D.Instance.UnregisterCollider(this); }
}