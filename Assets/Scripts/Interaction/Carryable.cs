using UnityEngine;

public class Carryable : MonoBehaviour
{
    [SerializeField] private string displayName;
    [SerializeField] private Collider collider;
    [SerializeField] private float restHeight = 0.1f;

    public virtual string DisplayName => displayName;
    public bool IsHeld { get; private set; }
    public Vector3 SurfacePoint => transform.position - Vector3.up * restHeight;

    public void PickUp(Transform hand)
    {
        IsHeld = true;
        collider.enabled = false;
        transform.SetParent(hand, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Place(Vector3 surfacePoint, Quaternion rotation)
    {
        IsHeld = false;
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(surfacePoint + Vector3.up * restHeight, rotation);
        foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>())
        {
            meshRenderer.renderingLayerMask = RenderingLayerMask.defaultRenderingLayerMask;
        }
        collider.enabled = true;
    }
}