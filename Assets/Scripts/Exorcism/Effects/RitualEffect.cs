using UnityEngine;

public abstract class RitualEffect : MonoBehaviour
{
    public virtual void RitualStarted(RitualController ritual)
    {
    }

    public virtual void IntensityChanged(float intensity)
    {
    }

    public virtual void StepStarted(RitualStep step)
    {
    }

    public virtual void StepSucceeded()
    {
    }

    public virtual void StepFailed()
    {
    }

    public virtual void BanishmentStarted()
    {
    }

    public virtual void Banished()
    {
    }

    public virtual void Aborted()
    {
    }

    public virtual void RitualEnded()
    {
    }
}
