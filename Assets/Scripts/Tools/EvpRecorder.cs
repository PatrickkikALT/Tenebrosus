using UnityEngine;

public class EvpRecorder : Tool
{
    [SerializeField] private GhostController ghost;
    [SerializeField] private Renderer recordLight;
    [SerializeField] private Vector2 listenIntervalRange = new(4f, 8f);
    [SerializeField] private float responseDuration = 4f;
    private string _lastRecording;

    private float _nextListen;
    private float _responseUntil;

    public override string Reading
    {
        get
        {
            if (IsOn && Time.time < _responseUntil)
                return $"\"{_lastRecording}\"";
            string state = IsOn ? "<i>Recording...</i>" : "Off";
            return _lastRecording == null ? state : $"{state}    Last: \"{_lastRecording}\"";
        }
    }

    private void Update()
    {
        recordLight.enabled = IsOn && Mathf.Repeat(Time.time, 1f) < 0.6f;
        if (!IsOn || Time.time < _nextListen) return;
        _nextListen = Time.time + Random.Range(listenIntervalRange.x, listenIntervalRange.y);
        if (ghost.TryGetEvpResponse(transform.position, out string response))
        {
            _lastRecording = response;
            _responseUntil = Time.time + responseDuration;
        }
    }

    public override void Use()
    {
        base.Use();
        _nextListen = Time.time + Random.Range(listenIntervalRange.x, listenIntervalRange.y);
    }
}