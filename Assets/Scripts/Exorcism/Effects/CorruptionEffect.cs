using System.Collections.Generic;
using UnityEngine;

public class CorruptionEffect : RitualEffect
{
    private static readonly int CorruptionId = Shader.PropertyToID("_Corruption");
    private static readonly int FlareId = Shader.PropertyToID("_Flare");

    [SerializeField] private Material overlay;
    [SerializeField] private float spreadDuration = 2.5f;
    [SerializeField] private float clearSpeed = 0.4f;
    [SerializeField] private float flareDecay = 1.2f;

    private readonly List<(Renderer renderer, Material[] original)> _targets = new();
    private MaterialPropertyBlock _block;
    private float _corruption;
    private bool _engaged;
    private float _flare;
    private float _target;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (_targets.Count == 0) return;
        float speed = _corruption < _target ? 1f / spreadDuration : clearSpeed;
        _corruption = Mathf.MoveTowards(_corruption, _target, speed * Time.deltaTime);
        _flare = Mathf.MoveTowards(_flare, 0f, flareDecay * Time.deltaTime);
        if (!_engaged && _corruption <= 0f)
        {
            Restore();
            return;
        }

        foreach ((Renderer renderer, Material[] original) entry in _targets)
        {
            Renderer target = entry.renderer;
            target.GetPropertyBlock(_block);
            _block.SetFloat(CorruptionId, _corruption);
            _block.SetFloat(FlareId, _flare);
            target.SetPropertyBlock(_block);
        }
    }

    public override void RitualStarted(RitualController ritual)
    {
        Restore();
        foreach (Renderer target in ritual.Target.Renderers)
        {
            Material[] original = target.sharedMaterials;
            Material[] corrupted = new Material[original.Length + 1];
            original.CopyTo(corrupted, 0);
            corrupted[original.Length] = overlay;
            target.sharedMaterials = corrupted;
            _targets.Add((target, original));
        }

        _engaged = true;
        _corruption = 0f;
        _target = 1f;
    }

    public override void IntensityChanged(float intensity)
    {
        if (_engaged) _target = 1f - intensity;
    }

    public override void StepFailed()
    {
        _flare = 1f;
    }

    public override void Aborted()
    {
        _flare = 1f;
        _target = 1f;
    }

    public override void RitualEnded()
    {
        _engaged = false;
        _target = 0f;
    }

    private void Restore()
    {
        foreach ((Renderer target, Material[] original) in _targets)
        {
            target.sharedMaterials = original;
            target.SetPropertyBlock(null);
        }

        _targets.Clear();
    }
}
