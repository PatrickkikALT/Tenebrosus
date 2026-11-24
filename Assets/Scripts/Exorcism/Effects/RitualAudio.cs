using UnityEngine;

public class RitualAudio : RitualEffect
{
    [SerializeField] private AudioSource drone;
    [SerializeField] private AudioSource whispers;
    [SerializeField] private AudioSource stinger;
    [SerializeField] private AnimationCurve droneVolume = AnimationCurve.Linear(0f, 0.15f, 1f, 0.8f);
    [SerializeField] private AnimationCurve whisperVolume = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Vector2 dronePitch = new(0.8f, 1.15f);
    [SerializeField] private float fadeSpeed = 0.5f;

    [Header("Stingers")] [SerializeField] private AudioClip startClip;
    [SerializeField] private AudioClip successClip;
    [SerializeField] private AudioClip failClip;
    [SerializeField] private AudioClip banishClip;
    [SerializeField] private AudioClip abortClip;

    private float _boost = 1f;
    private bool _engaged;
    private float _intensity;
    private float _master;

    private void Update()
    {
        _master = Mathf.MoveTowards(_master, _engaged ? 1f : 0f, fadeSpeed * Time.deltaTime);
        drone.volume = droneVolume.Evaluate(_intensity) * _master;
        drone.pitch = Mathf.Lerp(dronePitch.x, dronePitch.y, _intensity);
        whispers.volume = Mathf.Clamp01(whisperVolume.Evaluate(_intensity) * _boost) * _master;
        if (_engaged || _master > 0f || !drone.isPlaying) return;
        drone.Stop();
        whispers.Stop();
    }

    public override void RitualStarted(RitualController ritual)
    {
        _engaged = true;
        drone.Play();
        whispers.Play();
        stinger.PlayOneShot(startClip);
    }

    public override void IntensityChanged(float intensity)
    {
        _intensity = intensity;
    }

    public override void StepStarted(RitualStep step)
    {
        _boost = step.Effects.AudioBoost;
    }

    public override void StepSucceeded()
    {
        stinger.PlayOneShot(successClip);
    }

    public override void StepFailed()
    {
        stinger.PlayOneShot(failClip);
    }

    public override void Aborted()
    {
        stinger.PlayOneShot(abortClip);
    }

    public override void Banished()
    {
        stinger.PlayOneShot(banishClip);
        _engaged = false;
    }

    public override void RitualEnded()
    {
        _engaged = false;
    }
}
