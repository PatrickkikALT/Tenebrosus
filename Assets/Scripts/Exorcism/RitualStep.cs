using System;
using UnityEngine;

[Serializable]
public class RitualStep
{
    [SerializeField] [TextArea] private string phrase;
    [SerializeField] private string translation;
    [SerializeField] [Range(0f, 1f)] private float matchThreshold = 0.6f;
    [SerializeField] private bool requiresSacrifice;
    [SerializeField] private RitualItemType sacrifice;
    [SerializeField] private SacrificeTiming timing;
    [SerializeField] private float timeLimit = 30f;
    [SerializeField] private RitualEffectProfile effects;

    public string Phrase => phrase;
    public string Translation => translation;
    public float MatchThreshold => matchThreshold;
    public bool RequiresSacrifice => requiresSacrifice;
    public RitualItemType Sacrifice => sacrifice;
    public SacrificeTiming Timing => timing;
    public float TimeLimit => timeLimit;
    public RitualEffectProfile Effects => effects;
}
