using UnityEngine;

[CreateAssetMenu(menuName = "Tenebrosus/Exorcism Ritual", fileName = "ExorcismRitual")]
public class ExorcismRitualData : BookPage
{
    [SerializeField] private GhostType ghostType;
    [SerializeField] [TextArea] private string flavorLine;
    [SerializeField] private RitualStep[] steps;

    [Header("Failure")] [SerializeField] private RitualFailurePolicy failurePolicy = RitualFailurePolicy.RepeatStep;
    [SerializeField] [Min(1)] private int stepsLostOnFailure = 1;
    [SerializeField] [Min(1)] private int maxFailures = 3;
    [SerializeField] private float failureSanityLoss = 8f;
    [SerializeField] private float abortSanityLoss = 20f;

    public override string Title => ghostType.GhostName;
    public override string Subtitle => flavorLine;
    public GhostType GhostType => ghostType;
    public string FlavorLine => flavorLine;
    public RitualStep[] Steps => steps;
    public RitualFailurePolicy FailurePolicy => failurePolicy;
    public int StepsLostOnFailure => stepsLostOnFailure;
    public int MaxFailures => maxFailures;
    public float FailureSanityLoss => failureSanityLoss;
    public float AbortSanityLoss => abortSanityLoss;
}
