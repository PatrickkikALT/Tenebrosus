using UnityEngine;

public class HeadbobSystem : MonoBehaviour
{
    [Range(0.001f, 1f)] [SerializeField] private float amount = 0.02f;
    [Range(1f, 30f)] [SerializeField] private float frequency = 10.0f;
    [Range(10f, 100f)] [SerializeField] private float smooth = 10.0f;

    [SerializeField] private Movement playerMovement;
    private Vector3 _startPos;
    private float _weight;

    private void Start()
    {
        _startPos = transform.localPosition;
    }

    private void Update()
    {
        if (!playerMovement.canMove) return;

        float target = playerMovement.Input.sqrMagnitude > 0f ? 1f : 0f;
        _weight = Mathf.Lerp(_weight, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));

        Vector3 bob = new(
            Mathf.Cos(Time.time * frequency / 2f) * amount * 1.6f,
            Mathf.Sin(Time.time * frequency) * amount * 1.4f,
            0f);
        transform.localPosition = _startPos + bob * _weight;
    }
}
