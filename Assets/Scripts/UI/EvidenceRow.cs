using TMPro;
using UnityEngine;

public class EvidenceRow : MonoBehaviour
{
    [SerializeField] private EvidenceType evidence;
    [SerializeField] private Journal journal;
    [SerializeField] private GameObject checkMark;
    [SerializeField] private GameObject crossMark;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text stateText;

    public EvidenceType Evidence => evidence;
    public EvidenceState State { get; private set; }

    public void Cycle()
    {
        State = (EvidenceState)(((int)State + 1) % 3);
        checkMark.SetActive(State == EvidenceState.Found);
        crossMark.SetActive(State == EvidenceState.RuledOut);
        label.fontStyle = State == EvidenceState.RuledOut ? FontStyles.Strikethrough : FontStyles.Normal;
        label.alpha = State == EvidenceState.RuledOut ? 0.5f : 1f;
        stateText.text = State switch
        {
            EvidenceState.Found => "Found",
            EvidenceState.RuledOut => "Ruled out",
            _ => ""
        };
        journal.Refresh();
    }
}