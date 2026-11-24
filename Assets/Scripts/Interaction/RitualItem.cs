using UnityEngine;

public class RitualItem : Carryable
{
    [SerializeField] private RitualItemType itemType;
    [SerializeField] private Renderer[] renderers;

    public RitualItemType ItemType => itemType;
    public Renderer[] Renderers => renderers;
}