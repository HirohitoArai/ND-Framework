using GeometricAlgebra4D;
using UnityEngine;

public class Move4D : MonoBehaviour
{
    [Range(0.1f, 10)]
    public float MouseSensitivity = 3f;
    [Range(0.1f, 1)]
    public float Speed = 0.2f;

    private float _minX = -90f, _maxX = 90f;

    [SerializeField]
    private Vector4 Move;
    [SerializeField]
    private Vector4 Forward;
    private Quaternion _cameraRot;

    public GameObject Cam;
    private Transform4D _transform4d;

    private bool _cursorLock = true;

    void Start()
    {
        _transform4d = GetComponent<Transform4D>();
        _cameraRot = Cam.transform.localRotation;
    }
    void FixedUpdate()
    {
        float mouseX = Input.GetAxis("Mouse X") * MouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * MouseSensitivity;
        
        //Physically move the camera vertically
        _cameraRot *= Quaternion.Euler(-mouseY, 0, 0);
        _cameraRot = ClampRotation(_cameraRot);
        Cam.transform.localRotation = _cameraRot;

        Move = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;
        _transform4d.Position += (_transform4d.Forward * Move.z + _transform4d.Right * Move.x) * Speed;

        if (Mathf.Abs(mouseX) > 0.01f)
        {
            Bivector4D rotationPlane;
            if (Input.GetKey(KeyCode.LeftControl))
            {
                rotationPlane = Bivector4D.Rotxw;
            }
            else
            {
                rotationPlane = Bivector4D.Rotxz;
            }
            float angle = -mouseX;
            _transform4d.Rotate(rotationPlane, angle);
        }
        //Rotate the character horizontally, not the camera.
        Forward = _transform4d.Forward;
        UpdateCursorLock();
    }
    Quaternion ClampRotation(Quaternion q)
    {
        q.x /= q.w;
        q.y /= q.w;
        q.z /= q.w;
        q.w = 1f;

        float angleX = Mathf.Atan(q.x) * Mathf.Rad2Deg * 2f;
        angleX = Mathf.Clamp(angleX, _minX, _maxX);
        q.x = Mathf.Tan(angleX * Mathf.Deg2Rad * 0.5f);

        return q;
    }
    void UpdateCursorLock()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) _cursorLock = false;
        else if (Input.GetMouseButtonDown(0)) _cursorLock = true;

        Cursor.lockState = _cursorLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !_cursorLock;
    }
}
