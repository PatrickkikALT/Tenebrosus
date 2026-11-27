using UnityEngine;

[RequireComponent(typeof(Light))]
public class BackroomsLightFlicker : MonoBehaviour
{
    [SerializeField] private float speed = 9f;
    [SerializeField] [Range(0f, 1f)] private float threshold = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float dimmedFraction = 0.08f;

    private Light _light;
    private float _baseIntensity;
    private float _offset;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _offset = Random.value * 1000f;
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(_offset, Time.time * speed);
        _light.intensity = noise > threshold ? _baseIntensity : _baseIntensity * dimmedFraction;
    }
}
