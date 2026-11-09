using System.Linq;
using TMPro;
using UnityEngine;

public class GhostEntry : MonoBehaviour
{
    [SerializeField] private GhostType ghostType;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text evidenceText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text ritualText;
    [SerializeField] private Color foundColor = new(0.18f, 0.45f, 0.2f);
    [SerializeField] private Color ruledOutColor = new(0.62f, 0.16f, 0.12f);
    [SerializeField] [Range(0f, 1f)] private float ruledOutAlpha = 0.4f;

    public GhostType GhostType => ghostType;

    public bool Refresh(EvidenceType found, EvidenceType ruledOut)
    {
        bool possible = (ghostType.Evidence & found) == found && (ghostType.Evidence & ruledOut) == 0;
        canvasGroup.alpha = possible ? 1f : ruledOutAlpha;
        nameText.text = possible ? ghostType.GhostName : $"<s>{ghostType.GhostName}</s>";
        evidenceText.text = string.Join("   ·   ",
            EvidenceNames.All.Where(ghostType.Has).Select(evidence => FormatEvidence(evidence, found, ruledOut)));
        descriptionText.text = ghostType.Description;
        ritualText.text = "<b>Ritual</b>    " + string.Join("     ",
            ghostType.RitualSteps.Select((step, index) => $"{index + 1}. {RitualItemNames.Get(step)}"));
        return possible;
    }

    private string FormatEvidence(EvidenceType evidence, EvidenceType found, EvidenceType ruledOut)
    {
        string evidenceName = EvidenceNames.Get(evidence);
        if ((found & evidence) != 0)
            return $"<color=#{ColorUtility.ToHtmlStringRGB(foundColor)}><b>{evidenceName}</b></color>";
        if ((ruledOut & evidence) != 0)
            return $"<color=#{ColorUtility.ToHtmlStringRGB(ruledOutColor)}><s>{evidenceName}</s></color>";
        return evidenceName;
    }
}