using UnityEngine;

public class EmfReader : Tool
{
    [SerializeField] private GhostController ghost;
    [SerializeField] private Renderer[] leds;
    [SerializeField] private float updateInterval = 0.25f;

    private int _level;
    private float _nextUpdate;

    public override string Reading => IsOn
        ? $"<color=#FF5A3C>{new string('|', _level)}</color><color=#FFFFFF40>{new string('|', 5 - _level)}</color>   Level {_level}"
        : "Off";

    private void Update()
    {
        if (!IsOn)
        {
            SetLevel(0);
            return;
        }

        if (Time.time < _nextUpdate) return;
        _nextUpdate = Time.time + updateInterval;
        SetLevel(ghost.GetEmfLevel(transform.position));
    }

    private void SetLevel(int level)
    {
        _level = level;
        for (int i = 0; i < leds.Length; i++)
            leds[i].enabled = i < level;
    }
}