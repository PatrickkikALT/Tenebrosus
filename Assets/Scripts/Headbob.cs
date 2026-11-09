using UnityEngine;

public class HeadbobSystem : MonoBehaviour
{
    [Range(0.001f, 1f)] [SerializeField] private float amount = 0.02f;
    [Range(1f, 30f)] [SerializeField] private float frequency = 10.0f;
    [Range(10f, 100f)] [SerializeField] private float smooth = 10.0f;

    [SerializeField] private Movement playerMovement;
    private Vector3 _startPos;

    private void Start()
    {
        _startPos = transform.localPosition;
    }

    private void Update()
    {
        if (!playerMovement.canMove) return;
        CheckForHeadbobTrigger();
        StopHeadbob();
    }

    private void CheckForHeadbobTrigger()
    {
        float inputMagnitude = playerMovement.Input.magnitude;
        if (inputMagnitude > 0)
            StartHeadBob();
    }

    private Vector3 StartHeadBob()
    {
        Vector3 pos = Vector3.zero;
        pos.y += Mathf.Lerp(pos.y, Mathf.Sin(Time.time * frequency) * amount * 1.4f, smooth * Time.deltaTime);
        pos.x += Mathf.Lerp(pos.x, Mathf.Cos(Time.time * frequency / 2f) * amount * 1.6f, smooth * Time.deltaTime);
        transform.localPosition += pos;
        return pos;
    }

    private void StopHeadbob()
    {
        if (transform.localPosition == _startPos) return;
        transform.localPosition = Vector3.Lerp(transform.localPosition, _startPos, Time.deltaTime);
    }
}