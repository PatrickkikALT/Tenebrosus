using UnityEngine;

[CreateAssetMenu(menuName = "Tenebrosus/Ghost Type", fileName = "GhostType")]
public class GhostType : ScriptableObject
{
    [SerializeField] private string ghostName;
    [SerializeField] [TextArea] private string description;
    [SerializeField] private EvidenceType evidence;
    [SerializeField] private RitualItemType[] ritualSteps;

    public string GhostName => ghostName;
    public string Description => description;
    public EvidenceType Evidence => evidence;
    public RitualItemType[] RitualSteps => ritualSteps;

    public bool Has(EvidenceType type)
    {
        return (evidence & type) == type;
    }
}