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

    [Header("Backrooms")]
    [SerializeField] private BackroomsGenerator backrooms;
    [SerializeField] private float stalkStartDistance = 12f;
    [SerializeField] private float stalkMinDistance = 2.5f;
    [SerializeField] private float stalkStep = 2.5f;
    [SerializeField] private float minStalkDelay = 4f;
    [SerializeField] private float maxStalkDelay = 12f;
    [SerializeField] private float lingerTime = 8f;
    [SerializeField] private float stareDuration = 1.5f;
    [SerializeField] private float jumpscareDistance = 5f;
    [SerializeField] [Range(0f, 1f)] private float jumpscareChance = 0.15f;
    [SerializeField] private int placementAttempts = 16;
    [SerializeField] [Range(0f, 1f)] private float behindChance = 0.35f;
    [SerializeField] private float behindDistance = 1.3f;
    [SerializeField] [Range(0f, 1f)] private float behindJumpscareChance = 0.5f;
    [SerializeField] private float behindStareDuration = 0.5f;

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

    private bool IsInView(Vector3 point)
    {
        Vector3 viewport = playerCamera.WorldToViewportPoint(point);
        return viewport.z > 0f && viewport.x > -0.15f && viewport.x < 1.15f && viewport.y > -0.15f && viewport.y < 1.15f;
    }

    private void JumpInFront()
    {
        Transform cam = playerCamera.transform;
        Vector3 flatForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        transform.rotation = Quaternion.LookRotation(-flatForward, Vector3.up);
        Vector3 target = cam.position + cam.forward * scareDistance;
        transform.position += target - head.position;
    }

    private void FacePlayer()
    {
        Vector3 toPlayer = Vector3.ProjectOnPlane(playerCamera.transform.position - transform.position, Vector3.up);
        transform.rotation = Quaternion.LookRotation(toPlayer, Vector3.up);
    }

    private void SetPlayerLocked(bool locked)
    {
        playerMovement.canMove = !locked;
        playerLook.canMove = !locked;
        if (!locked) return;
        Rigidbody rb = playerMovement.GetComponent<Rigidbody>();
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    private IEnumerator Jumpscare()
    {
        SetPlayerLocked(true);
        body.enabled = true;
        JumpInFront();
        scream.Play();
        yield return new WaitForSeconds(scareDuration);
        SetPlayerLocked(false);
        body.enabled = false;
    }

    private bool TryPlaceOutOfSight(float distance)
    {
        Vector3 eye = playerCamera.transform.position;
        for (int i = 0; i < placementAttempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * distance;
            Vector3 candidate = eye + new Vector3(offset.x, 0f, offset.y);
            if (IsInView(candidate)) continue;
            if (Physics.Linecast(eye, candidate, occluders, QueryTriggerInteraction.Ignore)) continue;
            if (!Physics.Raycast(candidate, Vector3.down, out RaycastHit hit, 5f, occluders, QueryTriggerInteraction.Ignore)) continue;
            transform.position = hit.point;
            FacePlayer();
            return true;
        }

        return false;
    }

    private bool TryPlaceBehind()
    {
        Transform cam = playerCamera.transform;
        Vector3 flatForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 candidate = cam.position - flatForward * behindDistance;
        if (Physics.Linecast(cam.position, candidate, occluders, QueryTriggerInteraction.Ignore)) return false;
        if (!Physics.Raycast(candidate, Vector3.down, out RaycastHit hit, 5f, occluders, QueryTriggerInteraction.Ignore)) return false;
        transform.position = hit.point;
        FacePlayer();
        return true;
    }

    private IEnumerator Haunt()
    {
        yield return HauntHouse();
        yield return StalkBackrooms();
    }

    private IEnumerator HauntHouse()
    {
        while (true)
        {
            yield return new WaitUntil(() => IsSeen() || backrooms.IsActive);
            if (backrooms.IsActive) yield break;
            yield return new WaitForSeconds(reactionDelay);
            yield return Jumpscare();
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            yield return new WaitUntil(IsUnseen);
            body.enabled = true;
        }
    }

    private IEnumerator StalkBackrooms()
    {
        body.enabled = false;
        float distance = stalkStartDistance;

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minStalkDelay, maxStalkDelay));

            bool behind = Random.value < behindChance && TryPlaceBehind();
            if (!behind && !TryPlaceOutOfSight(distance)) continue;

            body.enabled = true;
            Vector3 followOffset = transform.position - playerMovement.transform.position;
            float timer = 0f;
            while (timer < lingerTime && !IsSeen())
            {
                timer += Time.deltaTime;
                if (behind)
                {
                    Vector3 follow = playerMovement.transform.position + followOffset;
                    transform.position = new Vector3(follow.x, transform.position.y, follow.z);
                }
                FacePlayer();
                yield return null;
            }

            if (!IsSeen())
            {
                body.enabled = false;
                if (!behind) distance = Mathf.Max(stalkMinDistance, distance - stalkStep);
                continue;
            }

            float scareChance = behind ? behindJumpscareChance : jumpscareChance;
            if ((!behind && distance <= jumpscareDistance) || Random.value < scareChance)
            {
                yield return new WaitForSeconds(reactionDelay);
                yield return Jumpscare();
                distance = stalkStartDistance;
                continue;
            }

            yield return new WaitForSeconds(behind ? behindStareDuration : stareDuration);
            body.enabled = false;
            if (!behind) distance = Mathf.Max(stalkMinDistance, distance - stalkStep);
        }
    }
}
