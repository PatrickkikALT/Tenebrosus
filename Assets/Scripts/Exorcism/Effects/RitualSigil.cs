using UnityEngine;

public class RitualSigil : RitualEffect
{
    private static readonly int RevealId = Shader.PropertyToID("_Reveal");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int FlashId = Shader.PropertyToID("_Flash");

    [SerializeField] private Renderer sigil;
    [SerializeField] private Light glow;
    [SerializeField] private VoiceRecognition voice;
    [SerializeField] [ColorUsage(false, true)] private Color startColor = new(4f, 1.6f, 0.4f);
    [SerializeField] private float drawDuration = 3f;
    [SerializeField] private float fadeDuration = 2.5f;
    [SerializeField] private float voiceGain = 25f;
    [SerializeField] private float voiceSmoothing = 12f;
    [SerializeField] private float colorSpeed = 1.5f;
    [SerializeField] private float beatDecay = 1.5f;
    [SerializeField] private float flashDecay = 1.5f;
    [SerializeField] private float glowIntensity = 600f;

    private MaterialPropertyBlock _block;
    private float _beat;
    private Color _color;
    private float _flash;
    private float _intensity;
    private float _pulse;
    private float _reveal;
    private float _revealTarget;
    private Color _targetColor;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        _color = startColor;
        _targetColor = startColor;
        Apply();
    }

    private void Update()
    {
        float speed = _revealTarget > _reveal ? 1f / drawDuration : 1f / fadeDuration;
        _reveal = Mathf.MoveTowards(_reveal, _revealTarget, speed * Time.deltaTime);
        float level = Mathf.Clamp01(voice.InputLevel * voiceGain);
        _pulse = Mathf.Lerp(_pulse, level, 1f - Mathf.Exp(-voiceSmoothing * Time.deltaTime));
        _color = Color.Lerp(_color, _targetColor, 1f - Mathf.Exp(-colorSpeed * Time.deltaTime));
        _beat = Mathf.MoveTowards(_beat, 0f, beatDecay * Time.deltaTime);
        _flash = Mathf.MoveTowards(_flash, 0f, flashDecay * Time.deltaTime);
        Apply();
    }

    public override void RitualStarted(RitualController ritual)
    {
        _revealTarget = 1f;
        _targetColor = startColor;
    }

    public override void IntensityChanged(float intensity)
    {
        _intensity = intensity;
    }

    public override void StepStarted(RitualStep step)
    {
        _targetColor = step.Effects.SigilColor;
    }

    public override void StepSucceeded()
    {
        _beat = 1f;
    }

    public override void StepFailed()
    {
        _flash = 1f;
    }

    public override void Aborted()
    {
        _flash = 1f;
    }

    public override void Banished()
    {
        _beat = 2f;
    }

    public override void RitualEnded()
    {
        _revealTarget = 0f;
    }

    private void Apply()
    {
        sigil.GetPropertyBlock(_block);
        _block.SetFloat(RevealId, _reveal);
        _block.SetFloat(PulseId, _pulse + _beat);
        _block.SetFloat(IntensityId, _intensity);
        _block.SetColor(ColorId, _color);
        _block.SetFloat(FlashId, _flash);
        sigil.SetPropertyBlock(_block);
        float peak = Mathf.Max(_color.maxColorComponent, 0.0001f);
        glow.color = Color.Lerp(_color / peak, Color.red, _flash);
        glow.intensity = glowIntensity * _reveal * (0.4f + _intensity + _pulse * 0.5f + _beat + _flash);
    }
}
