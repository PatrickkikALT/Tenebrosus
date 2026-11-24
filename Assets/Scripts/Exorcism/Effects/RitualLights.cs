using System.Collections.Generic;
using UnityEngine;

public class RitualLights : RitualEffect
{
    [SerializeField] private float maxFlickerRate = 12f;
    [SerializeField] [Range(0f, 1f)] private float colorShift = 0.85f;
    [SerializeField] private float failBlackout = 0.6f;

    private readonly List<(Light light, Color color)> _lights = new();
    private float _blackoutUntil;
    private bool _engaged;
    private float _flicker;
    private float _intensity;
    private Room _room;
    private Color _tint = Color.white;

    private void Update()
    {
        if (!_engaged) return;
        float offChance = Mathf.Clamp01(_intensity * _flicker * maxFlickerRate * Time.deltaTime);
        bool blackout = Time.time < _blackoutUntil;
        foreach ((Light light, Color color) entry in _lights)
        {
            entry.light.color = Color.Lerp(entry.color, _tint, _intensity * colorShift);
            if (_room.LightsOn) entry.light.enabled = !blackout && Random.value >= offChance;
        }
    }

    public override void RitualStarted(RitualController ritual)
    {
        Restore();
        _room = ritual.Room;
        foreach (Light light in _room.Lights)
            _lights.Add((light, light.color));
        _engaged = true;
    }

    public override void IntensityChanged(float intensity)
    {
        _intensity = intensity;
    }

    public override void StepStarted(RitualStep step)
    {
        _flicker = step.Effects.Flicker;
        _tint = step.Effects.LightColor;
    }

    public override void StepFailed()
    {
        _blackoutUntil = Time.time + failBlackout;
    }

    public override void Aborted()
    {
        _blackoutUntil = Time.time + failBlackout * 2f;
    }

    public override void Banished()
    {
        Restore();
    }

    public override void RitualEnded()
    {
        Restore();
    }

    private void Restore()
    {
        foreach ((Light light, Color color) entry in _lights)
            entry.light.color = entry.color;
        _lights.Clear();
        if (_room != null) _room.RestoreLights();
        _engaged = false;
    }
}
