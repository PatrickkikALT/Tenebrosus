using UnityEngine;

public class RitualEffectsDirector : MonoBehaviour
{
    [SerializeField] private RitualController ritual;
    [SerializeField] private RitualEffect[] effects;
    [SerializeField] private float riseSpeed = 0.35f;
    [SerializeField] private float fallSpeed = 0.6f;

    private float _target;

    public float Intensity { get; private set; }

    private void Update()
    {
        float speed = _target > Intensity ? riseSpeed : fallSpeed;
        float next = Mathf.MoveTowards(Intensity, _target, speed * Time.deltaTime);
        if (next == Intensity) return;
        Intensity = next;
        foreach (RitualEffect effect in effects)
            effect.IntensityChanged(Intensity);
    }

    public void SetIntensity(float intensity)
    {
        _target = intensity;
    }

    public void HandleRitualStarted()
    {
        foreach (RitualEffect effect in effects)
            effect.RitualStarted(ritual);
    }

    public void HandleStepStarted()
    {
        RitualStep step = ritual.CurrentStep;
        foreach (RitualEffect effect in effects)
            effect.StepStarted(step);
    }

    public void HandleStepSucceeded()
    {
        foreach (RitualEffect effect in effects)
            effect.StepSucceeded();
    }

    public void HandleStepFailed()
    {
        foreach (RitualEffect effect in effects)
            effect.StepFailed();
    }

    public void HandleBanishmentStarted()
    {
        foreach (RitualEffect effect in effects)
            effect.BanishmentStarted();
    }

    public void HandleBanished()
    {
        foreach (RitualEffect effect in effects)
            effect.Banished();
    }

    public void HandleAborted()
    {
        foreach (RitualEffect effect in effects)
            effect.Aborted();
    }

    public void HandleRitualEnded()
    {
        foreach (RitualEffect effect in effects)
            effect.RitualEnded();
    }
}
