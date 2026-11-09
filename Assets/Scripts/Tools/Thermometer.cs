using UnityEngine;

public class Thermometer : Tool
{
    [SerializeField] private GhostController ghost;
    [SerializeField] private float updateInterval = 1f;
    private bool _hasReading;
    private float _nextUpdate;

    private float _temperature;

    public override string Reading => !IsOn
        ? "Off"
        : _hasReading
            ? $"{_temperature:0.0} °C"
            : "Measuring...";

    private void Update()
    {
        if (!IsOn || Time.time < _nextUpdate) return;
        _nextUpdate = Time.time + updateInterval;
        _temperature = ghost.GetTemperature(transform.position);
        _hasReading = true;
    }

    public override void Use()
    {
        base.Use();
        _hasReading = false;
        _nextUpdate = Time.time + updateInterval;
    }
}