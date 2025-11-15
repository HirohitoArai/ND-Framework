using GeometricAlgebra3D;
using UnityEngine;

public class Transform3D : MonoBehaviour
{
    [SerializeField] private Vector3 _position = Vector3.zero;
    public Vector3 Position
    {
        get => _position;
        set
        {
            if (_position != value)
            {
                _position = value;
                IsDirtyforScript = true;
            }
        }
    }
    [SerializeField] private Bivector3D _rotation = Bivector3D.zero;
    public Bivector3D Rotation
    {
        get => _rotation;
        set
        {
            if (!_rotation.Equals(value))
            {
                _rotation = value;
                IsDirtyforScript = true; 
            }
        }
    }
    [SerializeField] private Vector3 _scale = Vector3.one;
    public Vector3 Scale
    {
        get => _scale;
        set
        {
            if (_scale != value)
            {
                _scale = value;
                IsDirtyforScript = true;
            }
        }
    }
    
    [HideInInspector]public bool IsDirty = true;
    [HideInInspector]public bool IsDirtyforScript = true;
    void OnValidate()
    {
        IsDirty = true;// If something changes in the inspector, flag it as "recalculated" anyway
    }
    private static readonly Vector4 VEC4_FORWARD = new(0, 0, 1);
    private static readonly Vector4 VEC4_RIGHT = new(1, 0, 0);
    private static readonly Vector4 VEC4_UP = new(0, 1, 0);
    //When each direction is called, the latest rotor is obtained and the basis is rotated and returned.
    public Vector3 Forward
    {
        get {
            GA3D.RotateVector(VEC4_FORWARD, this.GeometricRotation, out Vector3 result);
            return result;
        }
    }
    public Vector3 Right
    {
        get {
            GA3D.RotateVector(VEC4_RIGHT, this.GeometricRotation, out Vector3 result);
            return result;
        }
    }
    public Vector3 Up
    {
        get {
            GA3D.RotateVector(VEC4_UP, this.GeometricRotation, out Vector3 result);
            return result;
        }
    }

    //My current attitude expressed in geometric algebra
    private GA3D _localRotation = new(1f,0);
    public GA3D GeometricRotation {
        get {
            if (IsDirty) {
                UpdateRotationFromEuler();
                IsDirty = false;
            }
            return _localRotation;
        }
    }

    private static GA3D _deltaRotation = new();
    //Rotation with bivector as input
    public void Rotate(Bivector3D plane, float angleDegrees)
    {
        _deltaRotation.CreateRotor(plane, angleDegrees);
        this._localRotation.Multiply(_deltaRotation);
    
        IsDirty = false; 
    }

    private static GA3D _tempRotation = new();
    //Rotation with rotor as input
    public void RotateByGA3D(GA3D R)
    { 
        _tempRotation.Set(this._localRotation);
        this._localRotation.Set(R);
        this._localRotation.Multiply(_tempRotation);
    }

    private static GA3D rot_xy = new();
    private static GA3D rot_xz = new();
    private static GA3D rot_yz = new();
    private void UpdateRotationFromEuler()
    {
        // Three Euler angles generate three simple rotors
        rot_xy.CreateRotor(Bivector3D.Rotxy, Rotation.xy);
        rot_xz.CreateRotor(Bivector3D.Rotxz, Rotation.xz);
        rot_yz.CreateRotor(Bivector3D.Rotyz, Rotation.yz);
        // Composition in a fixed order
        _localRotation.Set(rot_yz).Multiply(rot_xz).Multiply(rot_xy);
    }
}
