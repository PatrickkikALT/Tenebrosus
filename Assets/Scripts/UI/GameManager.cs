using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Movement movement;
    [SerializeField] private CameraMovement cameraMovement;
    [SerializeField] private Rigidbody playerBody;
    [SerializeField] private GhostController ghost;
    [SerializeField] private Journal journal;
    [SerializeField] private GameObject endScreen;
    [SerializeField] private TMP_Text endTitleText;
    [SerializeField] private TMP_Text endBodyText;
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private bool showDebug;

    public bool IsPlaying { get; private set; } = true;

    private void Start()
    {
        endScreen.SetActive(false);
    }

    private void Update()
    {
        debugText.gameObject.SetActive(showDebug);
        if (!showDebug) return;
        debugText.text =
            $"{ghost.Type.GhostName} in {ghost.HauntedRoom.RoomName}, bound to {ghost.BoundObject.DisplayName}\n" +
            $"Anger {ghost.Anger:0}   Ritual {ghost.RitualProgress}/{ghost.Type.RitualSteps.Length}   Hunting {ghost.IsHunting}";
    }

    public void Win(string ghostName, string objectName)
    {
        End("BANISHED", $"You cleansed the {objectName} and drove out the {ghostName}.");
    }

    public void Lose(string reason)
    {
        End("YOU DIED",
            $"{reason}\nIt was a {ghost.Type.GhostName}, bound to the {ghost.BoundObject.DisplayName} in the {ghost.HauntedRoom.RoomName}.");
    }

    public void OnRestart(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || IsPlaying) return;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void End(string title, string body)
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        journal.Close();
        movement.canMove = false;
        cameraMovement.canMove = false;
        playerBody.linearVelocity = Vector3.zero;
        cameraMovement.UnlockCamera();
        endTitleText.text = title;
        endBodyText.text = body + "\n\n<size=70%>Press Space to play again</size>";
        endScreen.SetActive(true);
    }
}