using Geometry3D;
using GeometricAlgebra3D;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsBody3D : MonoBehaviour
{
    [Header("Physical Properties")]
    public float Mass = 1.0f;
    [Range(1, 10)]
    public int SubSteps = 1;//XPBD substep count
    private float _invSub;
    [Range(1, 10)]
    public int SolverIterations = 8; //XPBD iteration count
    [Range(0, 1)]
    public float ShapeMatchingCompliance = 0.01f; //Shape-retaining softness
    [Range(0.995f, 1)]
    public float Damping = 1;//damping coefficient
    [Range(0, 20)]
    public float Gravity;

    private Vector3 _gravity;
    private float _weight;//Ease of moving a point (the inverse of the point's weight)
    private float _invWeight;
    private int NumParticles;
    private float _invNumParticle;
    // Current physical state (p)
    private Vector3[] Positions;
    private Vector3[] Velocities;
    private Vector3 CurrentCenterOfMass;
    // Predicted position (p*)
    public Vector3[] PredictedPositions { get; private set; }

    private int[] DuplicateInfoMap;
    // Ideal shape for constraint (q)
    private Vector3[] _restLocalPositions; //Ideal shape (q) in local coordinates
    private Vector3 _restCenterOfMass_local; //Center of gravity of ideal shape in local coordinates
    private GA3D _rotationAmount;
    //Constraint variables
    private float[] _collisionLambdas;//Lambda for collision constraints
    private float[] _shapeMatchingLambdas;//Lambda for shape constraints
    private List<CollisionConstraint3D> _collisionConstraints;
    //Rotation adjustment variables
    private Vector3 _preCenter = Vector3.zero;
    private float _angularDamping = 1.0f;

    private Transform3D _objectTransform;
    private RenderMesh3D _renderMesh;

    //Tolerance
    private const float EPS = 1e-6f;
    void Awake()
    {
        _objectTransform = GetComponent<Transform3D>();
        _renderMesh = GetComponent<RenderMesh3D>();
        _rotationAmount = new(1f,0);
    }
    
    public void InitializePhysicsBody()
    {
        _collisionConstraints = new List<CollisionConstraint3D>();
        GenerateUniquePoints(_renderMesh.WorldVertices, out Vector3[] tempVertices,out int[] tempMap);
        DuplicateInfoMap = tempMap;
        Positions = tempVertices;

        NumParticles = Positions.Length;
        _invNumParticle = 1.0f / NumParticles;
        CurrentCenterOfMass = CalculateCenterOfMass(Positions);

        if (Mass == 0) Debug.LogError("It has zero mass");
        _weight = 1.0f / (Mass / NumParticles);
        _invWeight = 1.0f / _weight;

        Velocities = new Vector3[NumParticles];
        PredictedPositions = new Vector3[NumParticles];
        _restLocalPositions = new Vector3[NumParticles];
        _restCenterOfMass_local = Vector3.zero;

        for (int i = 0; i < NumParticles; i++)
        {
            PredictedPositions[i] = Positions[i]; // Copy the current position as the initial value
            _restLocalPositions[i] = Positions[i] - CurrentCenterOfMass;
        }

        _shapeMatchingLambdas = new float[NumParticles]; 
        _collisionLambdas = new float[NumParticles];
        _gravity = new Vector3(0, -Gravity, 0);
        if (PhysicsManager3D.Instance != null) PhysicsManager3D.Instance.RegisterBody(this);
    }
    public void GenerateUniquePoints(Vector3[] originalVertices, out Vector3[] uniqueVertices, out int[] DuplicateInfoMap)
    { 
        var uniquePositionsList = new List<Vector3>();
        var positionIndexMap = new Dictionary<Vector3, int>();
        DuplicateInfoMap = new int[_renderMesh.TotalVertexCount];

        for (int i = 0; i < _renderMesh.TotalVertexCount; i++)
        {
            Vector3 currentPos = originalVertices[i];
            if (!positionIndexMap.TryGetValue(currentPos, out int existingIndex))
            {
                existingIndex = uniquePositionsList.Count;
                positionIndexMap.Add(currentPos, existingIndex);
                uniquePositionsList.Add(currentPos);
            }
            DuplicateInfoMap[i] = existingIndex; 
        }
        uniqueVertices = uniquePositionsList.ToArray();
    }

    public void SimulateStep()
    {
        _invSub = 1.0f / SubSteps;
        float subDt = Time.fixedDeltaTime * _invSub;
        float invsubDt = 1.0f / subDt;
        for (int i = 0; i < SubSteps; i++)
        {
            PredictStep(subDt);
            for (int j = 0; j < SolverIterations; j++)
            {
                SolveInternalConstraintsOnce(invsubDt); 
            }
            UpdatePositionsAndVelocities(invsubDt);
        }
        UpdateStep();
        if (Time.frameCount % 5 == 0) RotationCheck();
    }
    private void PredictStep(float subDt)
    {
        Vector3 deltaVelocities = _gravity * subDt;
        for (int i = 0; i < NumParticles; i++)
        {
            Velocities[i] += deltaVelocities;
            PredictedPositions[i] = Positions[i] + Velocities[i] * subDt;
        }
        System.Array.Clear(_shapeMatchingLambdas, 0, _shapeMatchingLambdas.Length);
        System.Array.Clear(_collisionLambdas, 0, _collisionLambdas.Length);
    }
    private void SolveInternalConstraintsOnce(float invsubDt)
    {
        SolveShapeMatchingConstraint(invsubDt);
        SolveCollisionConstraint(_collisionConstraints);
    }
    private void UpdatePositionsAndVelocities(float invsubDt)
    {
        for (int i = 0; i < NumParticles; i++)
        {
            Velocities[i] = (PredictedPositions[i] - Positions[i]) * invsubDt * Damping; 
            Positions[i] = PredictedPositions[i];
        }
        _objectTransform.RotateByGA3D(_rotationAmount);
    }
    private void SolveShapeMatchingConstraint(float invsubDt)
    {
        float alpha_tilde = ShapeMatchingCompliance * invsubDt * invsubDt;
        float invDenominator = 1.0f / (_weight + alpha_tilde);
        this.CurrentCenterOfMass = CalculateCenterOfMass(PredictedPositions);
        for (int i = 0; i < NumParticles; i++)
        {
            Vector3 localRestVector = _restLocalPositions[i] - _restCenterOfMass_local;
            GA3D.RotateVector(localRestVector, _objectTransform.GeometricRotation, out Vector3 rotatedPosition);
            Vector3 goalPosition = rotatedPosition + CurrentCenterOfMass;

            Vector3 C_vector = PredictedPositions[i] - goalPosition;
            float sqrMagnitude = C_vector.sqrMagnitude;
            if (sqrMagnitude < EPS * EPS) continue;
            float C_scalar = Mathf.Sqrt(sqrMagnitude);
            float invMagnitude = 1.0f / C_scalar;
            Vector3 gradient = C_vector * invMagnitude;

            float currentLambda = _shapeMatchingLambdas[i];

            float numerator = -C_scalar - alpha_tilde * currentLambda;
            float deltaLambda = numerator * invDenominator;

            _shapeMatchingLambdas[i] += deltaLambda;
        
            Vector3 correction = deltaLambda * _weight * gradient;

            PredictedPositions[i] += correction;
        }
    }
    private void SolveCollisionConstraint(List<CollisionConstraint3D> constraints)
    {
        Vector3 allCorrections = Vector3.zero;
        Vector3 allLeverArm = Vector3.zero;
        float invCount = 1.0f / constraints.Count;
        foreach (var c in constraints)
        {
            int i = c.ParticleIndex;
            Vector3 normal = c.Normal.normalized;
            c.Depth = -Vector3.Dot(PredictedPositions[i] - c.Triangle.V0, normal);
            float depth = c.Depth;
            
            if (depth <= 0) continue;
            float deltaLambda = depth * _invWeight;
            float oldLambda = _collisionLambdas[i];
            _collisionLambdas[i] = Mathf.Max(0.0f, oldLambda + deltaLambda);
            deltaLambda = _collisionLambdas[i] - oldLambda;

            Vector3 correction = deltaLambda * normal * _weight;
            PredictedPositions[i] += correction;
            Positions[i] = PredictedPositions[i];

            allCorrections += correction;
            allLeverArm += PredictedPositions[i] - CurrentCenterOfMass;
        }

        if (allCorrections.sqrMagnitude *invCount > EPS)
        { 
            _rotationAmount.CreateRotorWithVector(allLeverArm, allCorrections, _angularDamping * _invSub);
        }
    }
    private void UpdateStep()
    {
        _objectTransform.Position = CalculateCenterOfMass(Positions);
        for (int i = 0; i < _renderMesh.TotalVertexCount; i++)
        {
            int physicsIndex = DuplicateInfoMap[i];
            _renderMesh.WorldVertices[i] = Positions[physicsIndex];
        }
    }
    private void RotationCheck()
    {
        float comDelta = (_preCenter - CurrentCenterOfMass).magnitude;
        if (comDelta < 0.02f) _angularDamping *= 1.0f - ((0.02f - comDelta) * 50f);
        else _angularDamping = 1.0f;
        _preCenter = CurrentCenterOfMass;
    }
    public Triangle[] GetTrianglesFromParticles()
    {
        var triList = new List<Triangle>();
        for (int i = 0; i < _renderMesh.TotalVertexCount; i += 3)
        {
            // Use mapping to convert to "physical" index
            int p_idx0 = this.DuplicateInfoMap[i];
            int p_idx1 = this.DuplicateInfoMap[i+1];
            int p_idx2 = this.DuplicateInfoMap[i+2];

            // Use the physical index to get the predicted position
            Vector3 p0 = this.PredictedPositions[p_idx0];
            Vector3 p1 = this.PredictedPositions[p_idx1];
            Vector3 p2 = this.PredictedPositions[p_idx2];
            Vector3 normal = Vector3.Cross(p0-p1, p0-p2).normalized;
            if (Vector3.Dot(normal, p0 - CurrentCenterOfMass) < 0) normal = -normal;
            triList.Add(new Triangle(p0,p1,p2,normal,p_idx0, p_idx1, p_idx2));
        }
        return triList.ToArray();
    }
    private Vector3 CalculateCenterOfMass(Vector3[] Positions)
    {
        Vector3 com = Vector3.zero;
        for (int i = 0; i < NumParticles; i++) com += Positions[i];
        return com * _invNumParticle;
    }
    public void ClearInternalCollisionConstraints(){ _collisionConstraints.Clear(); }
    public void AddCollisionConstraint(CollisionConstraint3D constraint){ _collisionConstraints.Add(constraint); }
    void OnDisable(){ if (PhysicsManager3D.Instance != null) PhysicsManager3D.Instance.UnregisterBody(this); }
}