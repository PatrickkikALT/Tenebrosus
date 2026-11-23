using System.Collections;
using UnityEngine;

public class DisappearingShadowFreddy : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Renderer body;
    [SerializeField] private Transform head;
    [SerializeField] private LayerMask occluders = 1;
    [SerializeField] private AudioSource scream;
    [SerializeField] private Movement playerMovement;
    [SerializeField] private CameraMovement playerLook;
    [SerializeField] private float reactionDelay = 0.3f;
    [SerializeField] private float scareDuration = 1.2f;
    [SerializeField] private float scareDistance = 0.5f;

    private readonly Plane[] _planes = new Plane[6];
    private Vector3 _homePosition;
    private Quaternion _homeRotation;

    private void Start()
    {
        _homePosition = transform.position;
        _homeRotation = transform.rotation;
        StartCoroutine(Haunt());
    }

    private bool IsSeen()
    {
        GeometryUtility.CalculateFrustumPlanes(playerCamera, _planes);
        Bounds bounds = body.bounds;
        if (!GeometryUtility.TestPlanesAABB(_planes, bounds)) return false;
        return !Physics.Linecast(playerCamera.transform.position, bounds.center, occluders, QueryTriggerInteraction.Ignore);
    }

    private bool IsUnseen()
    {
        return !IsSeen();
    }

    private void JumpInFront()
    {
        Transform cam = playerCamera.transform;
        Vector3 flatForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        transform.rotation = Quaternion.LookRotation(-flatForward, Vector3.up);
        Vector3 target = cam.position + cam.forward * scareDistance;
        transform.position += target - head.position;
    }

    private void SetPlayerLocked(bool locked)
    {
        playerMovement.canMove = !locked;
        playerLook.canMove = !locked;
        if (!locked) return;
        Rigidbody rb = playerMovement.GetComponent<Rigidbody>();
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    private IEnumerator Haunt()
    {
        while (true)
        {
            yield return new WaitUntil(IsSeen);
            yield return new WaitForSeconds(reactionDelay);
            SetPlayerLocked(true);
            JumpInFront();
            scream.Play();
            yield return new WaitForSeconds(scareDuration);
            SetPlayerLocked(false);
            body.enabled = false;
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            yield return new WaitUntil(IsUnseen);
            body.enabled = true;
        }
    }
}
