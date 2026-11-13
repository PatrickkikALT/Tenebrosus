using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Transform camera;
    [SerializeField] private Transform hand;
    [SerializeField] private Movement movement;
    [SerializeField] private GhostController ghost;
    [SerializeField] private int slotCount = 3;
    [SerializeField] private float range = 2.5f;
    [SerializeField] private LayerMask mask = 1;
    [SerializeField] private RenderingLayerMask lightLayer;
    [SerializeField] [Range(0f, 1f)] private float minPlaceNormalY = 0.7f;
    private bool _hasHit;
    private RaycastHit _hit;

    private Carryable[] _slots;

    public int SlotCount => slotCount;
    public int ActiveSlot { get; private set; }
    public Carryable Held => _slots[ActiveSlot];
    public string Prompt { get; private set; } = "";

    private bool HasEmptySlot => Array.IndexOf(_slots, null) >= 0;

    private void Awake()
    {
        _slots = new Carryable[slotCount];
    }

    private void Update()
    {
        _hasHit = Physics.Raycast(camera.position, camera.forward, out _hit, range, mask,
            QueryTriggerInteraction.Ignore);
        Prompt = BuildPrompt();
    }

    public Carryable GetSlot(int index)
    {
        return _slots[index];
    }

    public void OnUse(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !movement.canMove) return;
        if (Held is Tool tool)
            tool.Use();
    }

    public void OnNextSlot(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !movement.canMove) return;
        SelectSlot((ActiveSlot + 1) % slotCount);
    }

    public void OnPreviousSlot(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !movement.canMove) return;
        SelectSlot((ActiveSlot - 1 + slotCount) % slotCount);
    }

    public void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !movement.canMove || !_hasHit) return;
        if (_hit.collider.TryGetComponent(out LightSwitch lightSwitch))
        {
            lightSwitch.Toggle();
            return;
        }

        if (_hit.collider.TryGetComponent(out HeadCamera headCamera))
        {
            headCamera.Wear(camera);
            return;
        }

        if (_hit.collider.TryGetComponent(out Carryable carryable))
        {
            PickUp(carryable);
            return;
        }

        if (_hit.collider.TryGetComponent(out HauntableObject hauntable))
        {
            if (Held is RitualItem ritualItem)
                ghost.ApplyRitualItem(hauntable, ritualItem.ItemType);
            return;
        }

        if (Held != null && _hit.normal.y >= minPlaceNormalY)
        {
            Held.Place(_hit.point, Quaternion.Euler(0f, camera.eulerAngles.y, 0f));
            _slots[ActiveSlot] = null;
        }
    }

    private void SelectSlot(int index)
    {
        if (Held != null)
            Held.gameObject.SetActive(false);
        ActiveSlot = index;
        if (Held != null)
            Held.gameObject.SetActive(true);
    }

    private void PickUp(Carryable carryable)
    {
        if (Held != null)
        {
            if (HasEmptySlot)
                SelectSlot(Array.IndexOf(_slots, null));
            else
                Held.Place(carryable.SurfacePoint, carryable.transform.rotation);
        }

        carryable.PickUp(hand);
        //ew
        MeshRenderer[] renderers = carryable.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers) renderer.renderingLayerMask = lightLayer;
        _slots[ActiveSlot] = carryable;
    }
    

    private string BuildPrompt()
    {
        if (!_hasHit) return "";
        if (_hit.collider.TryGetComponent(out LightSwitch lightSwitch))
            return lightSwitch.Prompt;
        if (_hit.collider.TryGetComponent(out HeadCamera headCamera))
            return $"[E] Wear {headCamera.DisplayName}";
        if (_hit.collider.TryGetComponent(out Carryable carryable))
            return Held == null || HasEmptySlot
                ? $"[E] Pick up {carryable.DisplayName}"
                : $"[E] Swap for {carryable.DisplayName}";
        if (_hit.collider.TryGetComponent(out HauntableObject hauntable))
            return Held is RitualItem ritualItem
                ? $"[E] Use {ritualItem.DisplayName} on {hauntable.DisplayName}"
                : hauntable.DisplayName;
        if (Held != null && _hit.normal.y >= minPlaceNormalY)
            return $"[E] Place {Held.DisplayName}";
        return "";
    }
}