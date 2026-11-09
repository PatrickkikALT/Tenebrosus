using UnityEngine;

public class LightSwitch : MonoBehaviour
{
    [SerializeField] private Room room;
    [SerializeField] private Transform lever;
    [SerializeField] private float leverOffset = 0.2f;

    public string Prompt => room.LightsOn ? "[E] Turn off the lights" : "[E] Turn on the lights";

    private void Update()
    {
        Vector3 position = lever.localPosition;
        position.y = room.LightsOn ? leverOffset : -leverOffset;
        lever.localPosition = position;
    }

    public void Toggle()
    {
        room.ToggleLights();
    }
}