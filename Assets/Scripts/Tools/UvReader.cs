using UnityEngine;

public class UvReader : Tool
{
    [SerializeField] private Light light;
    [SerializeField] private LayerMask occluders = 1;

    public override string Reading => IsOn ? "On" : "Off";

    private void Update()
    {
        foreach (UvPrint print in UvPrint.All)
            print.SetRevealed(IsOn && IsLit(print.transform.position));
    }

    private void OnDisable()
    {
        foreach (UvPrint print in UvPrint.All)
            print.SetRevealed(false);
    }

    public override void Use()
    {
        base.Use();
        light.enabled = IsOn;
    }

    private bool IsLit(Vector3 point)
    {
        Vector3 origin = light.transform.position;
        Vector3 toPoint = point - origin;
        if (toPoint.magnitude > light.range ||
            Vector3.Angle(light.transform.forward, toPoint) > light.spotAngle * 0.5f)
            return false;
        return !Physics.Linecast(origin, point, occluders, QueryTriggerInteraction.Ignore);
    }
}