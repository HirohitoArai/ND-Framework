using Geometry3D;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]//Finish instantiation here first
public class PhysicsManager3D : MonoBehaviour
{
    // シングルトンインスタンス
    public static PhysicsManager3D Instance { get; private set; }
    public List<CollisionConstraint3D> _collisionConstraints { get; private set; }//Collision information

    private bool _isSimulationReady = false;
    private List<PhysicsBody3D> _allBodies;
    private List<Collider3D> _allColliders;

    private float EPS = 1e-4f;
    
    
    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
        // List initialization
        _allBodies = new List<PhysicsBody3D>();
        _allColliders = new List<Collider3D>();
        _collisionConstraints = new List<CollisionConstraint3D>();
    }
    
    void FixedUpdate()
    {
        //Is all initialization complete
        if (!_isSimulationReady)
        {
            foreach (var col in _allColliders)
            {
                if (col==null || !col.IsColliderInitialized) return;
            }
            _isSimulationReady =true;
            return;
        }
        
        foreach (var body in _allBodies)
        {
            body.ClearInternalCollisionConstraints();
        }
        //Collision calculation
        DetectCollisions();
        foreach (var constraint in _collisionConstraints)
        {
            constraint.Body.AddCollisionConstraint(constraint);
        }
        //Calculating the motion of an object
        foreach (var body in _allBodies)
        {
            body.SimulateStep();
        }
    }
    private void DetectCollisions()
    {
        _collisionConstraints.Clear();
        
        for (int i = 0; i < _allColliders.Count; i++)
        {
            for (int j = i + 1; j < _allColliders.Count; j++)
            {
                Collider3D colliderA = _allColliders[i];
                Collider3D colliderB = _allColliders[j];
                if(!colliderA.IsPhysicsDriven && !colliderB.IsPhysicsDriven)continue;
                if (!BoundingSphereCheck(colliderA, colliderB)) continue;
                if (AABBCheck(colliderA.BoundingBox, colliderB.BoundingBox))
                {
                    //Strict collision detection only for objects that pass through the sphere and AABB
                    GenerateContacts_VertexFace(colliderA, colliderB);
                    GenerateContacts_VertexFace(colliderB, colliderA);
                }
            } 
        }
    }

    private bool BoundingSphereCheck(Collider3D a, Collider3D b)
    {
        float distSq = (a.BoundingSphere.Center - b.BoundingSphere.Center).sqrMagnitude;
        float combinedRadius = a.BoundingSphere.Radius + b.BoundingSphere.Radius;
        return distSq <= combinedRadius * combinedRadius;
    }
    private bool AABBCheck(AABB3D a, AABB3D b)
    {
        bool overlapX = a.Min.x <= b.Max.x && a.Max.x >= b.Min.x;
        bool overlapY = a.Min.y <= b.Max.y && a.Max.y >= b.Min.y;
        bool overlapZ = a.Min.z <= b.Max.z && a.Max.z >= b.Min.z;

        if (!overlapX || !overlapY || !overlapZ) return false;
        return true;
    }
    
    private Triangle[] triangles;
    private Vector3[] verticesA;
    private List<(int, Triangle)> tempList = new();
    private List<(int, Triangle)> multiHitCandidates = new();
    private List<(int, Triangle)> finalContacts = new();
    private void GenerateContacts_VertexFace(Collider3D colliderA,Collider3D colliderB)
    {
        PhysicsBody3D bodyA;
        PhysicsBody3D bodyB;
        Vector3 AtoB = colliderB.BoundingSphere.Center - colliderA.BoundingSphere.Center;
        //Case classification based on the properties of the collision pair
        if (colliderA.IsPhysicsDriven && colliderB.IsPhysicsDriven)
        {
            bodyA = colliderA.PhysicsBody;
            bodyB = colliderB.PhysicsBody;
            triangles = colliderB.PhysicsBody.GetTrianglesFromParticles();
            verticesA = bodyA.PredictedPositions;
        }
        else if (colliderA.IsPhysicsDriven)
        {
            bodyA = colliderA.PhysicsBody;
            bodyB = null;
            triangles = colliderB.GetTrianglesFromParticles();
            verticesA = bodyA.PredictedPositions;
        }
        else
        {
            bodyA = null;
            bodyB = colliderB.PhysicsBody;
            triangles = colliderB.PhysicsBody.GetTrianglesFromParticles();
            verticesA = colliderA.GetRenderMeshInstance().WorldVertices;
        }

        multiHitCandidates.Clear();
        finalContacts.Clear();
        //Loop through each drawing vertex of bodyA
        for(int i =0; i < verticesA.Length; i++)
        {
            tempList.Clear();
            for (int triIndex = 0; triIndex < triangles.Length; triIndex++)
            {
                var triB = triangles[triIndex];
                if (PointTrianglePenetration(verticesA[i], triB, AtoB))
                {
                    tempList.Add((i,triB));
                }
            }

            if (tempList.Count == 1)
            {
                finalContacts.Add(tempList[0]);
            }
            else if(tempList.Count >= 2)
            {
                multiHitCandidates.AddRange(tempList);
            }
        }

        //What to do when a vertex touches multiple faces at the same time
        if (multiHitCandidates.Count >= 2)
        { 
            Vector3 refDir = verticesA[multiHitCandidates[0].Item1] - verticesA[multiHitCandidates[1].Item1];
            int CompareByComputedValue((int, Triangle) a, (int, Triangle) b)
            {
                float depthA = -Vector3.Dot((verticesA[a.Item1]-a.Item2.V0), a.Item2.Normal);
                float depthB = -Vector3.Dot((verticesA[b.Item1]-b.Item2.V0), b.Item2.Normal);
                return depthB.CompareTo(depthA);
            }

            multiHitCandidates.Sort(CompareByComputedValue);
            int added = 0;
            // Normal direction filter + up to 4 points
            for (int i = 0; i < multiHitCandidates.Count; i++)
            {
                var candidate = multiHitCandidates[i];
                Vector3 vertex = verticesA[candidate.Item1];
                Vector3 normal = candidate.Item2.Normal;
                // Check if the normal direction is almost perpendicular to refDir
                if (Mathf.Abs(Vector3.Dot(refDir.normalized, normal)) < 0.1f)
                {
                    finalContacts.Add(candidate);
                    added++;
                    if (added > 4) break;
                }
            }
        }
        multiHitCandidates.Clear();

        //Collect collision information into one class and register it in a list
        foreach (var constraint in finalContacts)
        { 
            int idx_A = constraint.Item1;
            Triangle triB = constraint.Item2;
            if (colliderA.IsPhysicsDriven)
            {
                _collisionConstraints.Add(new CollisionConstraint3D(bodyA, idx_A, triB.Normal, 0, triB));
            }     
            if (colliderB.IsPhysicsDriven)
            { 
                //Distribution of the embedded triangle to three vertices
                int[] triB_indices = { triB.Idx0, triB.Idx1, triB.Idx2 };
                triB.Normal *= 0.3333333f;

                foreach (int p_idx_B in triB_indices)
                {
                    _collisionConstraints.Add(new CollisionConstraint3D(bodyB, p_idx_B, -triB.Normal, 0, triB));// Normal opposite to A
                }
            }
        }
        finalContacts.Clear();
    }
    
    public bool PointTrianglePenetration(Vector3 p, Triangle triangle,Vector3 AtoB)
    {
        if (Vector3.Dot(AtoB, triangle.Normal) > EPS) return false;
        Vector3 p_minus_t0 = p - triangle.V0;
        float distToPlane = Vector3.Dot(p_minus_t0, triangle.Normal);
        if (distToPlane > EPS) return false;

        Vector3 v0 = triangle.V1 - triangle.V0;
        Vector3 v1 = triangle.V2 - triangle.V0;
        Vector3 v2 = p - triangle.V0;

        float dot00 = Vector3.Dot(v0, v0);
        float dot01 = Vector3.Dot(v0, v1);
        float dot02 = Vector3.Dot(v0, v2);
        float dot11 = Vector3.Dot(v1, v1);
        float dot12 = Vector3.Dot(v1, v2);

        float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        // 判定
        return ((u >= 0) && (v >= 0) && (u + v <= 1)) && (-distToPlane < 0.5f);
    }

    public void RegisterCollider(Collider3D col) {if (!_allColliders.Contains(col)) _allColliders.Add(col);}
    public void UnregisterCollider(Collider3D col) {_allColliders.Remove(col);}
    public void RegisterBody(PhysicsBody3D body) {if (!_allBodies.Contains(body)) _allBodies.Add(body);}
    public void UnregisterBody(PhysicsBody3D body) {_allBodies.Remove(body);}
}