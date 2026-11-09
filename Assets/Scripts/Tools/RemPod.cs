using UnityEngine;

public class RemPod : Tool
{
    [SerializeField] private GhostController ghost;
    [SerializeField] private Renderer armedLight;
    [SerializeField] private Renderer alarmLight;
    [SerializeField] private float alarmDuration = 2f;
    [SerializeField] private float blinkRate = 6f;

    private float _alarmUntil;

    private bool IsAlarming => Time.time < _alarmUntil;

    public override string Reading => !IsOn
        ? "Off"
        : IsAlarming
            ? "<color=#FF5A3C>ALERT - something is close</color>"
            : "Armed";

    private void Update()
    {
        if (IsOn && ghost.TriggersRemPod(transform.position))
            _alarmUntil = Time.time + alarmDuration;
        armedLight.enabled = IsOn && !IsAlarming;
        alarmLight.enabled = IsOn && IsAlarming && Mathf.Repeat(Time.time * blinkRate, 1f) < 0.5f;
    }
}