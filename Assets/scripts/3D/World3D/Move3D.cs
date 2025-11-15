using GeometricAlgebra3D;
using UnityEngine;

public class Move3D : MonoBehaviour
{
    [Range(0.1f, 10)]
    public float MouseSensitivity = 3f;
    [Range(0.1f, 1)]
    public float Speed = 0.2f;

    private float _minX = -90f, _maxX = 90f;

    [SerializeField]
    private Vector3 Move;
    [SerializeField]
    private Vector3 Forward;
    private Quaternion _cameraRot;

    public GameObject Cam;
    private Transform3D _transform3d;

    private bool cursorLock = true;

    void Start()
    {
        _transform3d = GetComponent<Transform3D>();
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
        _transform3d.Position += (_transform3d.Forward * Move.z + _transform3d.Right * Move.x) * Speed;

        if (Mathf.Abs(mouseX) > 0.01f)
        {
            Bivector3D rotationPlane;
            rotationPlane = Bivector3D.Rotxz;
            float angle = -mouseX;
            _transform3d.Rotate(rotationPlane, angle);
        }
        //Rotate the character horizontally, not the camera.
        Forward = _transform3d.Forward;
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
        if (Input.GetKeyDown(KeyCode.Escape)) cursorLock = false;
        else if (Input.GetMouseButtonDown(0)) cursorLock = true;

        Cursor.lockState = cursorLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !cursorLock;
    }
}
