using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Journal : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private EvidenceRow[] evidenceRows;
    [SerializeField] private GhostEntry[] ghostEntries;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private Movement movement;
    [SerializeField] private CameraMovement cameraMovement;
    [SerializeField] private Rigidbody playerBody;
    [SerializeField] private GameManager gameManager;

    public bool IsOpen { get; private set; }

    private void Start()
    {
        root.SetActive(false);
        Refresh();
    }

    public void OnToggle(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !gameManager.IsPlaying) return;
        IsOpen = !IsOpen;
        root.SetActive(IsOpen);
        movement.canMove = !IsOpen;
        cameraMovement.canMove = !IsOpen;
        if (IsOpen)
        {
            playerBody.linearVelocity = new Vector3(0f, playerBody.linearVelocity.y, 0f);
            cameraMovement.UnlockCamera();
        }
        else
        {
            cameraMovement.LockCamera();
        }
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        root.SetActive(false);
    }

    public void Refresh()
    {
        EvidenceType found = EvidenceType.None;
        EvidenceType ruledOut = EvidenceType.None;
        foreach (EvidenceRow row in evidenceRows)
            if (row.State == EvidenceState.Found) found |= row.Evidence;
            else if (row.State == EvidenceState.RuledOut) ruledOut |= row.Evidence;

        int possibleCount = 0;
        GhostEntry lastPossible = null;
        foreach (GhostEntry entry in ghostEntries)
        {
            if (!entry.Refresh(found, ruledOut)) continue;
            possibleCount++;
            lastPossible = entry;
        }

        summaryText.text = possibleCount switch
        {
            0 => "No ghost matches this evidence. Recheck your findings.",
            1 => $"Only one ghost fits the evidence: <b>{lastPossible.GhostType.GhostName}</b>",
            _ => $"{possibleCount} of {ghostEntries.Length} ghosts still fit the evidence"
        };
    }
}