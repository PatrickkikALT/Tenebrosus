using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float mouseSensitivity = 1;
    [Tooltip("Degrees per second at full stick deflection.")]
    [SerializeField] private float gamepadDegreesPerSecond = 180;
    public bool canMove = true;
    private float _verticalLookRotation;
    private float _yaw;
    private Vector2 _stick;

    public Quaternion FlatRotation => transform.rotation * Quaternion.Euler(0, _yaw, 0);

    private void Start()
    {
        LockCamera();
    }

    private void Update()
    {
        if (!canMove || _stick == Vector2.zero) return;
        Look(_stick * (gamepadDegreesPerSecond * Time.deltaTime));
    }

    public void LockCamera()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void UnlockCamera()
    {
        Cursor.lockState = CursorLockMode.None;
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 value = ctx.ReadValue<Vector2>();
        if (ctx.control.device is Gamepad)
        {
            _stick = value;
            return;
        }

        if (!ctx.performed || !canMove) return;
        Look(value * mouseSensitivity);
    }

    private void Look(Vector2 delta)
    {
        _yaw += delta.x;
        _verticalLookRotation = Mathf.Clamp(_verticalLookRotation - delta.y, -90f, 90f);
        cameraHolder.localRotation = Quaternion.Euler(_verticalLookRotation, _yaw, 0);
    }
}
