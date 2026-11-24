using UnityEngine;

public class RitualParticles : RitualEffect
{
    [SerializeField] private ParticleSystem[] ambient;
    [SerializeField] private float minRate = 4f;
    [SerializeField] private float maxRate = 60f;
    [SerializeField] private ParticleSystem failBurst;
    [SerializeField] private int failBurstCount = 60;
    [SerializeField] private ParticleSystem banishBurst;
    [SerializeField] private int banishBurstCount = 300;

    public override void RitualStarted(RitualController ritual)
    {
        foreach (ParticleSystem system in ambient)
            system.Play();
        failBurst.Play();
        banishBurst.Play();
    }

    public override void IntensityChanged(float intensity)
    {
        foreach (ParticleSystem system in ambient)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier = Mathf.Lerp(minRate, maxRate, intensity);
        }
    }

    public override void StepFailed()
    {
        failBurst.Emit(failBurstCount);
    }

    public override void Aborted()
    {
        failBurst.Emit(failBurstCount * 2);
    }

    public override void Banished()
    {
        banishBurst.Emit(banishBurstCount);
    }

    public override void RitualEnded()
    {
        foreach (ParticleSystem system in ambient)
            system.Stop();
        failBurst.Stop();
        banishBurst.Stop();
    }
}
