using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NoclipPortal : MonoBehaviour
{
    private static readonly int NoiseId = Shader.PropertyToID("_Noise");
    private static readonly int TearId = Shader.PropertyToID("_Tear");
    private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
    private static readonly int NoiseLineId = Shader.PropertyToID("_NoiseLine");
    private static readonly int AberrationId = Shader.PropertyToID("_Aberration");

    [SerializeField] private BackroomsGenerator backrooms;
    [SerializeField] private float clipDepth = 0.05f;
    [SerializeField] private Material vhsShader;
    [SerializeField] private float rampUpDuration = 0.6f;
    [SerializeField] private float holdDuration = 0.15f;
    [SerializeField] private float rampDownDuration = 1.4f;
    [SerializeField] private float peakNoise = 0.8f;
    [SerializeField] private float peakTear = 2f;
    [SerializeField] private float peakDistortion = 3f;
    [SerializeField] private float peakNoiseLine = 1f;
    [SerializeField] private float peakAberration = -8f;

    private Collider _collider;
    private bool _transitioning;
    private float _baseNoise;
    private float _baseTear;
    private float _baseDistortion;
    private float _baseNoiseLine;
    private float _baseAberration;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _baseNoise = vhsShader.GetFloat(NoiseId);
        _baseTear = vhsShader.GetFloat(TearId);
        _baseDistortion = vhsShader.GetFloat(DistortionId);
        _baseNoiseLine = vhsShader.GetFloat(NoiseLineId);
        _baseAberration = vhsShader.GetFloat(AberrationId);
    }

    private void OnDestroy()
    {
        SetGlitch(0f);
    }

    private void OnTriggerStay(Collider other)
    {
        if (_transitioning) return;
        Rigidbody body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent(out Movement _)) return;
        if (Vector3.Distance(_collider.ClosestPoint(body.position), body.position) > clipDepth) return;

        StartCoroutine(Transition(body));
        
    }

    private IEnumerator Transition(Rigidbody body)
    {
        _transitioning = true;

        SetGlitch(1f);
        backrooms.Enter(body);
        
        yield return new WaitForSeconds(holdDuration);

        for (float t = 0f; t < rampDownDuration; t += Time.deltaTime)
        {
            SetGlitch(1f - t / rampDownDuration);
            yield return null;
        }

        SetGlitch(0f);
        _transitioning = false;
    }

    private void SetGlitch(float amount)
    {
        float eased = amount * amount * (3f - 2f * amount);
        vhsShader.SetFloat(NoiseId, Mathf.Lerp(_baseNoise, peakNoise, eased));
        vhsShader.SetFloat(TearId, Mathf.Lerp(_baseTear, peakTear, eased));
        vhsShader.SetFloat(DistortionId, Mathf.Lerp(_baseDistortion, peakDistortion, eased));
        vhsShader.SetFloat(NoiseLineId, Mathf.Lerp(_baseNoiseLine, peakNoiseLine, eased));
        vhsShader.SetFloat(AberrationId, Mathf.Lerp(_baseAberration, peakAberration, eased));
    }
}
