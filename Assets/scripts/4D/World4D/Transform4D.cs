using GeometricAlgebra4D;
using UnityEngine;

public class Transform4D : MonoBehaviour
{
    [SerializeField] private Vector4 _position = Vector4.zero;
    public Vector4 Position
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
    [SerializeField] private Bivector4D _rotation = Bivector4D.zero;
    public Bivector4D Rotation
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
    [SerializeField] private Vector4 _scale = Vector4.one;
    public Vector4 Scale
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
        IsDirty = true;// インスペクターで何かが変更されたら、とにかく「再計算が必要」のフラグを立てる
    }

    private static readonly Vector4 VEC4_FORWARD = new(0, 0, 1, 0);
    private static readonly Vector4 VEC4_RIGHT = new(1, 0, 0, 0);
    private static readonly Vector4 VEC4_UP = new(0, 1, 0, 0);
    private static readonly Vector4 VEC4_ANA = new(0, 0, 0, 1);

    //各方向が呼ばれた瞬間に最新のローターを取得し、それで基底を回転させて返す
    public Vector4 Forward
    {
        get {
            GA4D.RotateVector(VEC4_FORWARD, this.GeometricRotation, out Vector4 result);
            return result;
        }
    }
    public Vector4 Right
    {
        get {
            GA4D.RotateVector(VEC4_RIGHT, this.GeometricRotation, out Vector4 result);
            return result;
        }
    }
    public Vector4 Up
    {
        get {
            GA4D.RotateVector(VEC4_UP, this.GeometricRotation, out Vector4 result);
            return result;
        }
    }
    public Vector4 Ana
    {
        get {
            GA4D.RotateVector(VEC4_ANA, this.GeometricRotation, out Vector4 result);
            return result;
        }
    }

    //幾何代数で表された現在の自分の姿勢
    private GA4D _localRotation = new(1f,0);
    public GA4D GeometricRotation {
        get {
            if (IsDirty) {
                UpdateRotationFromEuler();
                IsDirty = false;
            }
            return _localRotation;
        }
    }

    private static GA4D _deltaRotation = new();
    //バイベクターを入力とする回転
    public void Rotate(Bivector4D plane, float angleDegrees)
    {
        _deltaRotation.CreateRotor(plane, angleDegrees);
        this._localRotation.Multiply(_deltaRotation);
    
        IsDirty = false; 
    }

    private static GA4D _tempRotation = new();
    //ローターを入力とする回転
    public void RotateByGA4D(GA4D R)
    { 
        _tempRotation.Set(this._localRotation);
        this._localRotation.Set(R);
        this._localRotation.Multiply(_tempRotation);
    }

    private static GA4D rot_xy = new();
    private static GA4D rot_xz = new();
    private static GA4D rot_xw = new();
    private static GA4D rot_yz = new();
    private static GA4D rot_yw = new();
    private static GA4D rot_zw = new();
    private void UpdateRotationFromEuler()
    {
        // 6つのオイラー角から、6つの単純回転ローターを生成
        rot_xy.CreateRotor(Bivector4D.Rotxy, Rotation.xy);
        rot_xz.CreateRotor(Bivector4D.Rotxz, Rotation.xz);
        rot_xw.CreateRotor(Bivector4D.Rotxw, Rotation.xw);
        rot_yz.CreateRotor(Bivector4D.Rotyz, Rotation.yz);
        rot_yw.CreateRotor(Bivector4D.Rotyw, Rotation.yw);
        rot_zw.CreateRotor(Bivector4D.Rotzw, Rotation.zw);
        // 決められた順序で合成
        _localRotation.Set(rot_zw).Multiply(rot_yw).Multiply(rot_yz).Multiply(rot_xw).Multiply(rot_xz).Multiply(rot_xy);
    }
}