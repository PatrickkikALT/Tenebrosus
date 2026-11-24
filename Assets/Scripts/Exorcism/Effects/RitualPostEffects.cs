using UnityEngine;
using UnityEngine.Rendering;

public class RitualPostEffects : RitualEffect
{
    private static readonly int HazeId = Shader.PropertyToID("_RitualHaze");
    private static readonly int ShockId = Shader.PropertyToID("_RitualShock");
    private static readonly int FlashId = Shader.PropertyToID("_RitualFlash");
    private static readonly int FlashColorId = Shader.PropertyToID("_RitualFlashColor");
    private static readonly int BurstId = Shader.PropertyToID("_RitualBurst");
    private static readonly int SanityHitId = Shader.PropertyToID("_RitualSanityHit");

    [SerializeField] private Volume volume;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private AnimationCurve weightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float hazeStrength = 1f;
    [SerializeField] private float hazeSpeed = 2f;
    [SerializeField] private float shockDuration = 1.2f;
    [SerializeField] private float successShockStrength = 0.25f;
    [SerializeField] private float failShockStrength = 0.7f;
    [SerializeField] private float banishShockStrength = 1.5f;
    [SerializeField] private float flashDecay = 2.5f;
    [SerializeField] private float burstDecay = 0.6f;
    [SerializeField] private float sanityHitDecay = 0.8f;
    [SerializeField] private Color failColor = new(1f, 0.05f, 0.02f);
    [SerializeField] private Color burstColor = new(1f, 0.95f, 0.8f);

    private float _burst;
    private float _flash;
    private Color _flashColor;
    private float _haze;
    private float _intensity;
    private float _profileHaze;
    private RitualController _ritual;
    private float _sanityHit;
    private float _shockAge = 1f;
    private float _shockStrength;

    private void Update()
    {
        float delta = Time.deltaTime;
        volume.weight = weightCurve.Evaluate(_intensity);
        float hazeTarget = _intensity * (0.4f + _profileHaze) * hazeStrength;
        _haze = Mathf.Lerp(_haze, hazeTarget, 1f - Mathf.Exp(-hazeSpeed * delta));
        _flash = Mathf.MoveTowards(_flash, 0f, flashDecay * delta);
        _burst = Mathf.MoveTowards(_burst, 0f, burstDecay * delta);
        _sanityHit = Mathf.MoveTowards(_sanityHit, 0f, sanityHitDecay * delta);
        Vector4 shock = Vector4.zero;
        if (_shockAge < 1f)
        {
            _shockAge = Mathf.Min(1f, _shockAge + delta / shockDuration);
            Vector3 viewport = viewCamera.WorldToViewportPoint(_ritual.Site.position + Vector3.up);
            Vector2 center = viewport.z > 0f ? new Vector2(viewport.x, viewport.y) : new Vector2(0.5f, 0.5f);
            shock = new Vector4(center.x, center.y, _shockAge * 1.6f, _shockStrength * (1f - _shockAge));
        }

        Shader.SetGlobalFloat(HazeId, _haze);
        Shader.SetGlobalVector(ShockId, shock);
        Shader.SetGlobalFloat(FlashId, _flash);
        Shader.SetGlobalColor(FlashColorId, _flashColor);
        Shader.SetGlobalFloat(BurstId, _burst);
        Shader.SetGlobalFloat(SanityHitId, _sanityHit);
    }

    public override void RitualStarted(RitualController ritual)
    {
        _ritual = ritual;
    }

    public override void IntensityChanged(float intensity)
    {
        _intensity = intensity;
    }

    public override void StepStarted(RitualStep step)
    {
        _profileHaze = step.Effects.Haze;
    }

    public override void StepSucceeded()
    {
        Shock(successShockStrength);
    }

    public override void StepFailed()
    {
        _flash = 1f;
        _flashColor = failColor;
        _sanityHit = 1f;
        Shock(failShockStrength);
    }

    public override void Aborted()
    {
        _flash = 1f;
        _flashColor = failColor;
        _sanityHit = 1f;
        Shock(banishShockStrength);
    }

    public override void Banished()
    {
        _burst = 1f;
        _flash = 0.6f;
        _flashColor = burstColor;
        Shock(banishShockStrength);
    }

    private void Shock(float strength)
    {
        _shockAge = 0f;
        _shockStrength = strength;
    }
}
