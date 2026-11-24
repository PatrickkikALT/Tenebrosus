using UnityEngine;

public class GhostBanishmentEffect : RitualEffect
{
    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

    [SerializeField] private Renderer ghostRenderer;
    [SerializeField] private Material dissolveMaterial;
    [SerializeField] private Light burstLight;
    [SerializeField] private float dissolveDuration = 4.5f;
    [SerializeField] [Range(0f, 1f)] private float dissolveBeforeBurst = 0.8f;
    [SerializeField] private float writheAmount = 0.2f;
    [SerializeField] private float writheSpeed = 25f;
    [SerializeField] private float burstIntensity = 40000f;
    [SerializeField] private float burstDecay = 2.5f;

    private MaterialPropertyBlock _block;
    private float _burst;
    private bool _dissolving;
    private Material[] _original;
    private Vector3 _scale;
    private float _start;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        _burst = Mathf.MoveTowards(_burst, 0f, burstDecay * Time.deltaTime);
        burstLight.intensity = burstIntensity * _burst * _burst;
        if (!_dissolving) return;
        float t = Mathf.Clamp01((Time.time - _start) / dissolveDuration);
        ghostRenderer.GetPropertyBlock(_block);
        _block.SetFloat(DissolveId, t * dissolveBeforeBurst);
        ghostRenderer.SetPropertyBlock(_block);
        float time = Time.time * writheSpeed;
        Vector3 writhe = new(
            Mathf.PerlinNoise(time, 0.3f) - 0.5f,
            Mathf.PerlinNoise(0.6f, time) - 0.5f,
            Mathf.PerlinNoise(time, time) - 0.5f);
        ghostRenderer.transform.localScale = Vector3.Scale(_scale, Vector3.one + writhe * (writheAmount * t * 2f));
    }

    public override void BanishmentStarted()
    {
        _original = ghostRenderer.sharedMaterials;
        Material[] materials = new Material[_original.Length];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = dissolveMaterial;
        ghostRenderer.sharedMaterials = materials;
        _scale = ghostRenderer.transform.localScale;
        _start = Time.time;
        _dissolving = true;
    }

    public override void Banished()
    {
        burstLight.transform.position = ghostRenderer.bounds.center;
        _burst = 1f;
        Restore();
    }

    public override void RitualEnded()
    {
        Restore();
    }

    private void Restore()
    {
        if (!_dissolving) return;
        _dissolving = false;
        ghostRenderer.enabled = false;
        ghostRenderer.sharedMaterials = _original;
        ghostRenderer.SetPropertyBlock(null);
        ghostRenderer.transform.localScale = _scale;
    }
}
