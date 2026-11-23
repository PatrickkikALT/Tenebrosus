using UnityEngine;

public class Sanity : MonoBehaviour
{
    [SerializeField] private GhostController ghost;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private float baseDrain = 0.08f;
    [SerializeField] private float hauntedRoomMultiplier = 2.5f;
    [SerializeField] private float darknessMultiplier = 1.5f;

    public float Value { get; private set; } = 100f;
    public float Insanity => 1f - Value / 100f;

    private void Update()
    {
        if (!gameManager.IsPlaying) return;
        Room room = ghost.FindRoom(transform.position);
        if (room == null) return;
        float drain = baseDrain;
        if (room == ghost.HauntedRoom) drain *= hauntedRoomMultiplier;
        if (!room.LightsOn) drain *= darknessMultiplier;
        Drain(drain * Time.deltaTime);
    }

    public void Drain(float amount)
    {
        Value = Mathf.Max(0f, Value - amount);
    }
}
