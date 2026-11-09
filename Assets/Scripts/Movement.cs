using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Movement : MonoBehaviour
{
    [Header("General")] public bool canMove = true;

    [SerializeField] private float speed;

    [Header("Jumping")] [SerializeField] private bool jumping;

    [SerializeField] private float jumpStrength;
    [SerializeField] private Transform groundCheckTransform;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float overlapSphereSize;
    private float _fallVel;
    private Vector2 _input;
    private Rigidbody _rb;

    public Vector2 Input => _input;

    public bool OnGround =>
        Physics.OverlapSphereNonAlloc(groundCheckTransform.position, overlapSphereSize, new Collider[10],
            groundLayers) > 0;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (!canMove) return;
        Vector3 dir = transform.TransformDirection(new Vector3(_input.x, 0, _input.y));
        _rb.linearVelocity = new Vector3(dir.x * speed, _rb.linearVelocity.y, dir.z * speed);
        if (jumping) UpdateJumpMomentum();
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        _input = ctx.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !canMove) return;
        if (OnGround)
        {
            jumping = true;
            _rb.linearVelocity = new Vector3(0, jumpStrength, 0);
        }
    }

    private void UpdateJumpMomentum()
    {
        if (_rb.linearVelocity.y < 0.5)
        {
            _rb.linearVelocity -= new Vector3(0, _fallVel * Time.deltaTime, 0);

            if (OnGround) jumping = false;
        }
    }
}