public abstract class Tool : Carryable
{
    public bool IsOn { get; private set; }
    public abstract string Reading { get; }

    public virtual void Use()
    {
        IsOn = !IsOn;
    }
}