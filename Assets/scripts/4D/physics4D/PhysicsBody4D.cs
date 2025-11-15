using Geometry4D;
using GeometricAlgebra4D;
using System.Collections.Generic;
using UnityEngine;
public class PhysicsBody4D : MonoBehaviour
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

    private Vector4 _gravity;
    private float _weight;//Ease of moving a point (the inverse of the point's weight)
    private float _invWeight;
    private int NumParticles;
    private float _invNumParticle;
    // Current physical state (p)
    private Vector4[] Positions;
    private Vector4[] Velocities;
    private Vector4 CurrentCenterOfMass;
    // Predicted position (p*)
    public Vector4[] PredictedPositions { get; private set; }
    private int[] DuplicateInfoMap;
    // Ideal shape for constraint (q)
    private Vector4[] _restLocalPositions; //Ideal shape (q) in local coordinates
    private Vector4 _restLocalCenterOfMass; //Center of gravity of ideal shape in local coordinates
    private GA4D _rotationAmount;
    //Constraint variables
    private float[] _collisionLambdas;//Lambda for collision constraints
    private float[] _shapeMatchingLambdas;//Lambda for shape constraints
    private List<CollisionConstraint4D> _collisionConstraints;
    //Rotation adjustment variables
    private Vector4 _preCenter = Vector4.zero;
    private float _angularDamping = 1.0f;

    private Transform4D _objectTransform;
    private RenderMesh4D _renderMesh;

    //Tolerance
    private const float EPS = 1e-6f;
    void Awake()
    {
        _objectTransform = GetComponent<Transform4D>();
        _renderMesh = GetComponent<RenderMesh4D>();
        _rotationAmount = new(1f,0);
    }
    
    public void InitializePhysicsBody()
    {
        RenderMesh4D renderMesh = GetComponent<RenderMesh4D>();
        _collisionConstraints = new List<CollisionConstraint4D>();
        GenerateUniquePoints(renderMesh.WorldVertices, out Vector4[] tempVertices,out int[] tempMap);
        DuplicateInfoMap = tempMap;
        Positions = tempVertices;

        NumParticles = Positions.Length;
        _invNumParticle = 1.0f / NumParticles;
        CurrentCenterOfMass = CalculateCenterOfMass(Positions);

        if (Mass == 0) Debug.LogError("éøó Ç™É[ÉçÇ≈Ç∑");
        _weight = 1.0f / (Mass / NumParticles);
        _invWeight = 1.0f / _weight;

        Velocities = new Vector4[NumParticles];
        PredictedPositions = new Vector4[NumParticles];
        _restLocalPositions = new Vector4[NumParticles];
        _restLocalCenterOfMass = Vector4.zero;
        for (int i = 0; i < PredictedPositions.Length; i++)
        {
            PredictedPositions[i] = Positions[i]; // Copy the current position as the initial value
            _restLocalPositions[i] = Positions[i] - CurrentCenterOfMass;
        }

        _shapeMatchingLambdas = new float[NumParticles]; 
        _collisionLambdas = new float[NumParticles];
        _gravity = new Vector3(0, -Gravity, 0);
        if (PhysicsManager4D.Instance != null) PhysicsManager4D.Instance.RegisterBody(this);
    }
    public void GenerateUniquePoints(Vector4[] originalVertices, out Vector4[] uniqueVertices, out int[] DuplicateInfoMap)
    { 
        var uniquePositionsList = new List<Vector4>();
        var positionIndexMap = new Dictionary<Vector4, int>();
        DuplicateInfoMap = new int[_renderMesh.TotalVertexCount];

        for (int i = 0; i < _renderMesh.TotalVertexCount; i++)
        {
            Vector4 currentPos = originalVertices[i];
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
        Vector4 deltaVelocities = _gravity * subDt;
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
        _objectTransform.RotateByGA4D(_rotationAmount);
    }
    private void SolveShapeMatchingConstraint(float invsubDt)
    {
        float alpha_tilde = ShapeMatchingCompliance * invsubDt * invsubDt;
        float invDenominator = 1.0f / (_weight + alpha_tilde);
        this.CurrentCenterOfMass = CalculateCenterOfMass(PredictedPositions);
        for (int i = 0; i < NumParticles; i++)
        {
            Vector4 localRestVector = _restLocalPositions[i] - _restLocalCenterOfMass;
            GA4D.RotateVector(localRestVector, _objectTransform.GeometricRotation, out Vector4 rotatedPosition);
            Vector4 goalPosition = rotatedPosition + CurrentCenterOfMass;

            Vector4 C_vector = PredictedPositions[i] - goalPosition;
            float sqrMagnitude = C_vector.sqrMagnitude;
            if (sqrMagnitude < EPS * EPS) continue;
            float C_scalar = Mathf.Sqrt(sqrMagnitude);
            float invMagnitude = 1.0f / C_scalar;
            Vector4 gradient = C_vector * invMagnitude; 

            float currentLambda = _shapeMatchingLambdas[i];

            float numerator = -C_scalar - alpha_tilde * currentLambda;
            float deltaLambda = numerator * invDenominator;
            _shapeMatchingLambdas[i] += deltaLambda;
        
            Vector4 correction = deltaLambda * _weight * gradient;

            PredictedPositions[i] += correction;
        }
    }
    private void SolveCollisionConstraint(List<CollisionConstraint4D> constraints)
    {
        Vector4 allCorrections = Vector4.zero;
        Vector4 allLeverArm = Vector4.zero;
        float invCount = 1.0f / constraints.Count;
        foreach (var c in constraints)
        {
            int i = c.ParticleIndex;
            Vector4 normal = c.Normal.normalized;
            c.Depth = -Vector4.Dot(PredictedPositions[i] - c.Tetrahedron.V0, normal);
            float depth = c.Depth;
            
            if (depth <= 0) continue;
            float deltaLambda = depth * _invWeight;
            float oldLambda = _collisionLambdas[i];
            _collisionLambdas[i] = Mathf.Max(0.0f, oldLambda + deltaLambda);
            deltaLambda = _collisionLambdas[i] - oldLambda;

            Vector4 correction = deltaLambda * normal * _weight;
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

    public Tetrahedron[] GetTetrahedronsFromParticles()
    {
        var tetraList = new List<Tetrahedron>();
        for (int i = 0; i < _renderMesh.TotalVertexCount; i += 4)
        {
            // Use mapping to convert to "physical" index
            int Idx0 = this.DuplicateInfoMap[i];
            int Idx1 = this.DuplicateInfoMap[i+1];
            int Idx2 = this.DuplicateInfoMap[i+2];
            int Idx3 = this.DuplicateInfoMap[i+3];

            // Use the physical index to get the predicted position
            Vector4 p0 = this.PredictedPositions[Idx0];
            Vector4 p1 = this.PredictedPositions[Idx1];
            Vector4 p2 = this.PredictedPositions[Idx2];
            Vector4 p3 = this.PredictedPositions[Idx3];

            Vector4 normal = GA4D.Cross4D(p0 -p1, p0 - p2, p0 -p3).normalized;
            if (Vector4.Dot(normal, p0 - CurrentCenterOfMass) < 0) normal = -normal;
            tetraList.Add(new Tetrahedron(p0, p1, p2, p3, normal, Idx0, Idx1, Idx2, Idx3));
        }
        return tetraList.ToArray();
    }

    private Vector4 CalculateCenterOfMass(Vector4[] Positions)
    {
        Vector4 com = Vector4.zero;
        for (int i = 0; i < NumParticles; i++) com += Positions[i];
        return com * _invNumParticle;
    }
    public void ClearInternalCollisionConstraints(){ _collisionConstraints.Clear(); }
    public void AddCollisionConstraint(CollisionConstraint4D constraint){ _collisionConstraints.Add(constraint); }
    void OnDisable(){ if (PhysicsManager4D.Instance != null) PhysicsManager4D.Instance.UnregisterBody(this); }
}