using UnityEngine;

public class Room : MonoBehaviour
{
    [SerializeField] private string roomName;
    [SerializeField] private BoxCollider area;
    [SerializeField] private float baseTemperature = 18f;
    [SerializeField] private bool lightsOnAtStart;
    [SerializeField] private Light[] lights;
    [SerializeField] private HauntableObject[] hauntables;
    [SerializeField] private Rigidbody[] throwables;

    public string RoomName => roomName;
    public float BaseTemperature => baseTemperature;
    public bool LightsOn { get; private set; }
    public Light[] Lights => lights;
    public HauntableObject[] Hauntables => hauntables;
    public Rigidbody[] Throwables => throwables;

    private void Start()
    {
        SetLights(lightsOnAtStart);
    }

    public bool Contains(Vector3 position)
    {
        return area.bounds.Contains(position);
    }

    public Vector3 RandomPoint(float height)
    {
        Bounds bounds = area.bounds;
        return new Vector3(
            Random.Range(bounds.min.x + 0.5f, bounds.max.x - 0.5f),
            height,
            Random.Range(bounds.min.z + 0.5f, bounds.max.z - 0.5f));
    }

    public void ToggleLights()
    {
        SetLights(!LightsOn);
    }

    public void SetLights(bool on)
    {
        LightsOn = on;
        RestoreLights();
    }

    public void RestoreLights()
    {
        foreach (Light roomLight in lights)
            roomLight.enabled = LightsOn;
    }
}