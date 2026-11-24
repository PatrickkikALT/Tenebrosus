using System.Collections.Generic;
using UnityEngine;

public class RitualLevitation : RitualEffect
{
    [SerializeField] private float radius = 6f;
    [SerializeField] [Range(0f, 1f)] private float threshold = 0.25f;
    [SerializeField] private float hoverHeight = 0.6f;
    [SerializeField] private float spring = 12f;
    [SerializeField] private float damping = 3f;
    [SerializeField] private float shake = 3f;
    [SerializeField] private float failForce = 2f;
    [SerializeField] private float banishForce = 5f;

    private readonly List<(Rigidbody body, float restHeight)> _bodies = new();
    private bool _engaged;
    private float _intensity;
    private float _levitation;
    private Vector3 _center;

    private void FixedUpdate()
    {
        if (!_engaged) return;
        float lift = Mathf.Clamp01((_intensity - threshold) / (1f - threshold)) * _levitation;
        if (lift <= 0f) return;
        foreach ((Rigidbody body, float restHeight) entry in _bodies)
        {
            Rigidbody body = entry.body;
            float error = entry.restHeight + hoverHeight * lift - body.position.y;
            Vector3 hover = Vector3.up * (error * spring - body.linearVelocity.y * damping);
            body.AddForce(-Physics.gravity + hover + Random.insideUnitSphere * (shake * lift), ForceMode.Acceleration);
            body.AddTorque(Random.insideUnitSphere * (shake * lift), ForceMode.Acceleration);
        }
    }

    public override void RitualStarted(RitualController ritual)
    {
        _bodies.Clear();
        _center = ritual.Site.position;
        foreach (Rigidbody body in ritual.Room.Throwables)
            if (Vector3.Distance(body.position, _center) <= radius)
                _bodies.Add((body, body.position.y));
        _engaged = true;
    }

    public override void IntensityChanged(float intensity)
    {
        _intensity = intensity;
    }

    public override void StepStarted(RitualStep step)
    {
        _levitation = step.Effects.Levitation;
    }

    public override void StepFailed()
    {
        Push(failForce);
    }

    public override void Aborted()
    {
        Push(banishForce);
        _engaged = false;
    }

    public override void Banished()
    {
        Push(banishForce);
        _engaged = false;
    }

    public override void RitualEnded()
    {
        _engaged = false;
    }

    private void Push(float force)
    {
        foreach ((Rigidbody body, float restHeight) entry in _bodies)
        {
            Vector3 away = Vector3.ProjectOnPlane(entry.body.position - _center, Vector3.up).normalized;
            entry.body.AddForce((away + Vector3.up * 0.5f) * force, ForceMode.VelocityChange);
            entry.body.AddTorque(Random.insideUnitSphere * force, ForceMode.VelocityChange);
        }
    }
}
