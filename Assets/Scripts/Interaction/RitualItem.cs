using UnityEngine;

public class RitualItem : Carryable
{
    [SerializeField] private RitualItemType itemType;

    public RitualItemType ItemType => itemType;
}