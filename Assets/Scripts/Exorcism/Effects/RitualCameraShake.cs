using UnityEngine;

public class RitualCameraShake : RitualEffect
{
    [SerializeField] private float maxAngle = 2.5f;
    [SerializeField] private float frequency = 18f;
    [SerializeField] private float ambientShare = 0.35f;
    [SerializeField] private float traumaDecay = 1.2f;
    [SerializeField] private float failTrauma = 0.8f;
    [SerializeField] private float banishTrauma = 1f;

    private bool _engaged;
    private float _intensity;
    private float _profileShake;
    private float _trauma;

    private void LateUpdate()
    {
        _trauma = Mathf.MoveTowards(_trauma, 0f, traumaDecay * Time.deltaTime);
        float ambient = _engaged ? _intensity * _profileShake * ambientShare : 0f;
        float amount = Mathf.Clamp01(ambient + _trauma);
        amount *= amount * maxAngle;
        float t = Time.time * frequency;
        transform.localRotation = Quaternion.Euler(
            (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * amount,
            (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * amount,
            (Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * amount);
    }

    public override void RitualStarted(RitualController ritual)
    {
        _engaged = true;
    }

    public override void IntensityChanged(float intensity)
    {
        _intensity = intensity;
    }

    public override void StepStarted(RitualStep step)
    {
        _profileShake = step.Effects.Shake;
    }

    public override void StepFailed()
    {
        _trauma = Mathf.Max(_trauma, failTrauma);
    }

    public override void Aborted()
    {
        _trauma = 1f;
    }

    public override void BanishmentStarted()
    {
        _trauma = Mathf.Max(_trauma, banishTrauma * 0.5f);
    }

    public override void Banished()
    {
        _trauma = banishTrauma;
        _engaged = false;
    }

    public override void RitualEnded()
    {
        _engaged = false;
    }
}
