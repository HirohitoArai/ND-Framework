using Geometry3D;
using Geometry4D;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]//Finish instantiation here first
public class PhysicsManager4D : MonoBehaviour
{
    // シングルトンインスタンス
    public static PhysicsManager4D Instance { get; private set; }
    public List<CollisionConstraint4D> _collisionConstraints { get; private set; } //Collision information

    private bool _isSimulationReady;
    private List<PhysicsBody4D> _allBodies;
    private List<Collider4D> _allColliders;

    private float EPS = 1e-4f;
    
    
    void Awake()
    {
        _isSimulationReady = false;
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
        // List initialization
        _allBodies = new List<PhysicsBody4D>();
        _allColliders = new List<Collider4D>();
        _collisionConstraints = new List<CollisionConstraint4D>();
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
                Collider4D colliderA = _allColliders[i];
                Collider4D colliderB = _allColliders[j];
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

    private bool BoundingSphereCheck(Collider4D a, Collider4D b)
    {
        float distSq = (a.BoundingSphere.Center - b.BoundingSphere.Center).sqrMagnitude;
        float combinedRadius = a.BoundingSphere.Radius + b.BoundingSphere.Radius;
        return distSq <= combinedRadius * combinedRadius;
    }
    private bool AABBCheck(AABB4D a, AABB4D b)
    {
        bool overlapX = a.Min.x <= b.Max.x && a.Max.x >= b.Min.x;
        bool overlapY = a.Min.y <= b.Max.y && a.Max.y >= b.Min.y;
        bool overlapZ = a.Min.z <= b.Max.z && a.Max.z >= b.Min.z;
        bool overlapW = a.Min.w <= b.Max.w && a.Max.w >= b.Min.w;

        if (!overlapX || !overlapY || !overlapZ || !overlapW) return false;
        return true;
    }
    
    private Tetrahedron[] tetrahedrons;
    private Vector4[] verticesA;
    private List<(int, Tetrahedron)> tempList = new();
    private List<(int, Tetrahedron)> multiHitCandidates = new();
    private List<(int, Tetrahedron)> finalContacts = new();
    private void GenerateContacts_VertexFace(Collider4D colliderA,Collider4D colliderB)
    {
        PhysicsBody4D bodyA;
        PhysicsBody4D bodyB;
        Vector4 AtoB = colliderB.BoundingSphere.Center - colliderA.BoundingSphere.Center;
        //Case classification based on the properties of the collision pair
        if (colliderA.IsPhysicsDriven && colliderB.IsPhysicsDriven)
        {
            bodyA = colliderA.PhysicsBody;
            bodyB = colliderB.PhysicsBody;
            tetrahedrons = colliderB.PhysicsBody.GetTetrahedronsFromParticles();
            verticesA = bodyA.PredictedPositions;
        }
        else if (colliderA.IsPhysicsDriven)
        {
            bodyA = colliderA.PhysicsBody;
            bodyB = null;
            tetrahedrons = colliderB.GetTetrahedronsFromParticles();
            verticesA = bodyA.PredictedPositions;
        }
        else
        {
            bodyA = null;
            bodyB = colliderB.PhysicsBody;
            tetrahedrons = colliderB.PhysicsBody.GetTetrahedronsFromParticles();
            verticesA = colliderA.GetRenderMeshInstance().WorldVertices;
        }

        multiHitCandidates.Clear();
        finalContacts.Clear();
        //Loop through each drawing vertex of bodyA
        for(int i =0; i < verticesA.Length; i++)
        {
            tempList.Clear();
            for (int triIndex = 0; triIndex < tetrahedrons.Length; triIndex++)
            {
                var triB = tetrahedrons[triIndex];
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
            var tempMultiHitCandidates = new List<(int, Tetrahedron)>();
            Vector4 refDir = verticesA[multiHitCandidates[0].Item1] - verticesA[multiHitCandidates[1].Item1];
            int CompareByComputedValue((int, Tetrahedron) a, (int, Tetrahedron) b)
            {
                float depthA = -Vector4.Dot((verticesA[a.Item1]-a.Item2.V0), a.Item2.Normal);
                float depthB = -Vector4.Dot((verticesA[b.Item1]-b.Item2.V0), b.Item2.Normal);
                return depthB.CompareTo(depthA);
            }

            multiHitCandidates.Sort(CompareByComputedValue);
            int added = 0;
            // Normal direction filter + up to 4 points
            for (int i = 0; i < multiHitCandidates.Count; i++)
            {
                var candidate = multiHitCandidates[i];
                Vector4 vertex = verticesA[candidate.Item1];
                Vector4 normal = candidate.Item2.Normal;
                // Check if the normal direction is almost perpendicular to refDir
                if (Mathf.Abs(Vector4.Dot(refDir.normalized, normal)) < 0.1f)
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
            Tetrahedron triB = constraint.Item2;
            if (colliderA.IsPhysicsDriven)
            { 
                _collisionConstraints.Add(new CollisionConstraint4D(bodyA, idx_A, triB.Normal,0,triB));
            }     
            if (colliderB.IsPhysicsDriven)
            {
                //Distribution of the embedded triangle to three vertices
                int[] triB_indices = { triB.Idx0, triB.Idx1, triB.Idx2 };
                triB.Normal *=0.3333333f;

                foreach (int p_idx_B in triB_indices)
                {
                    _collisionConstraints.Add(new CollisionConstraint4D(bodyB, p_idx_B, -triB.Normal,0,triB));// Normal opposite to A
                }
            }
        }
        finalContacts.Clear();
    }
    
    public bool PointTrianglePenetration(Vector4 p, Tetrahedron tetrahedron, Vector4 AtoB)
    {
        if (Vector4.Dot(AtoB, tetrahedron.Normal) > EPS) return false;

        Vector4 p_minus_t0 = p - tetrahedron.V0;
        float distToPlane = Vector4.Dot(p_minus_t0, tetrahedron.Normal);
        if (distToPlane > EPS) return false;

        Vector4 v0 = tetrahedron.V1 - tetrahedron.V0;
        Vector4 v1 = tetrahedron.V2 - tetrahedron.V0;
        Vector4 v2 = tetrahedron.V3 - tetrahedron.V0;
        Vector4 v3 = p - tetrahedron.V0;


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
        // calculate w
        float detW = dot00 * (dot11 * dot32 - dot12 * dot31) -
                        dot01 * (dot01 * dot32 - dot12 * dot30) +
                        dot02 * (dot01 * dot31 - dot11 * dot30);
        float w = detW * invDenom;
        return ((u >= -EPS) && (v >= -EPS) && (w >= -EPS) && (u + v + w <= 1.0f + EPS)) && (-distToPlane < 0.5f);
    }
    public void RegisterCollider(Collider4D col) {if (!_allColliders.Contains(col)) _allColliders.Add(col);}
    public void UnregisterCollider(Collider4D col) {_allColliders.Remove(col);}
    public void RegisterBody(PhysicsBody4D body) {if (!_allBodies.Contains(body)) _allBodies.Add(body);}
    public void UnregisterBody(PhysicsBody4D body) {_allBodies.Remove(body);}
}
