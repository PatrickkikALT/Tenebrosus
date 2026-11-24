using UnityEngine;

[CreateAssetMenu(menuName = "Tenebrosus/Ghost Type", fileName = "GhostType")]
public class GhostType : ScriptableObject
{
    [SerializeField] private string ghostName;
    [SerializeField] [TextArea] private string description;
    [SerializeField] [TextArea] private string tell;
    [SerializeField] private EvidenceType evidence;

    [Header("Activity Tells")] [SerializeField] [Min(0f)] private float flickerWeight = 1f;
    [SerializeField] [Min(0f)] private float throwWeight = 1f;
    [SerializeField] [Min(0f)] private float lightsOffWeight = 1f;
    [SerializeField] [Min(0f)] private float glimpseWeight = 1f;
    [SerializeField] [Min(0.1f)] private float activityRate = 1f;
    [SerializeField] [Min(0f)] private float throwForceMultiplier = 1f;

    [Header("Hunt Tells")] [SerializeField] [Min(0.1f)] private float huntSpeedMultiplier = 1f;
    [SerializeField] [Min(0.1f)] private float huntDurationMultiplier = 1f;

    [Header("Sound Tells")] [SerializeField] private string[] evpResponses;
    [SerializeField] private AudioClip omenClip;
    [SerializeField] [Min(1)] private int omenRepeats = 1;
    [SerializeField] private float omenInterval = 0.6f;
    [SerializeField] private AudioClip ambientClip;
    [SerializeField] private Vector2 ambientInterval = new(30f, 60f);

    public string GhostName => ghostName;
    public string Description => description;
    public string Tell => tell;
    public EvidenceType Evidence => evidence;
    public float FlickerWeight => flickerWeight;
    public float ThrowWeight => throwWeight;
    public float LightsOffWeight => lightsOffWeight;
    public float GlimpseWeight => glimpseWeight;
    public float ActivityRate => activityRate;
    public float ThrowForceMultiplier => throwForceMultiplier;
    public float HuntSpeedMultiplier => huntSpeedMultiplier;
    public float HuntDurationMultiplier => huntDurationMultiplier;
    public string[] EvpResponses => evpResponses;
    public AudioClip OmenClip => omenClip;
    public int OmenRepeats => omenRepeats;
    public float OmenInterval => omenInterval;
    public AudioClip AmbientClip => ambientClip;
    public Vector2 AmbientInterval => ambientInterval;

    public bool Has(EvidenceType type)
    {
        return (evidence & type) == type;
    }
}
