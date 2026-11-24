using UnityEngine;

[CreateAssetMenu(menuName = "Tenebrosus/Ritual Effect Profile", fileName = "RitualEffectProfile")]
public class RitualEffectProfile : ScriptableObject
{
    [SerializeField] [ColorUsage(false, true)] private Color sigilColor = new(4f, 1.6f, 0.4f);
    [SerializeField] private Color lightColor = new(1f, 0.55f, 0.35f);
    [SerializeField] [Range(0f, 1f)] private float flicker = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float shake = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float levitation = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float haze = 0.3f;
    [SerializeField] [Range(0f, 2f)] private float audioBoost = 1f;

    public Color SigilColor => sigilColor;
    public Color LightColor => lightColor;
    public float Flicker => flicker;
    public float Shake => shake;
    public float Levitation => levitation;
    public float Haze => haze;
    public float AudioBoost => audioBoost;
}
