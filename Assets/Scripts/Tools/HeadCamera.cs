using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class HeadCamera : Carryable
{
    [SerializeField] private GameObject model;
    [SerializeField] private Light infraredLight;
    [SerializeField] private Volume nightVisionVolume;
    [SerializeField] private Movement movement;
    [SerializeField] private float duration = 10f;
    [SerializeField] private float rechargeTime = 15f;

    private float _activeUntil;
    private float _readyAt;

    public bool IsWorn { get; private set; }
    public bool NightVisionActive => Time.time < _activeUntil;

    public string Status
    {
        get
        {
            if (NightVisionActive) return $"Night vision: {_activeUntil - Time.time:0}s left";
            if (Time.time < _readyAt) return $"Night vision: recharging {_readyAt - Time.time:0}s";
            return "Night vision: ready [N]";
        }
    }

    private void Update()
    {
        bool active = NightVisionActive;
        infraredLight.enabled = active;
        nightVisionVolume.weight = active ? 1f : 0f;
    }

    public void Wear(Transform mount)
    {
        PickUp(mount);
        IsWorn = true;
        model.SetActive(false);
    }

    public void OnToggle(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !IsWorn || !movement.canMove) return;
        if (NightVisionActive)
        {
            _activeUntil = Time.time;
            _readyAt = Time.time + rechargeTime;
            return;
        }

        if (Time.time < _readyAt) return;
        _activeUntil = Time.time + duration;
        _readyAt = _activeUntil + rechargeTime;
    }
}