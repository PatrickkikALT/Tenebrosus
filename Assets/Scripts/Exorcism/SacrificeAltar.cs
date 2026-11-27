using UnityEngine;

public class SacrificeAltar : MonoBehaviour
{
    [SerializeField] private RitualController ritual;
    [SerializeField] private SacrificeDissolve dissolve;
    [SerializeField] private Transform offeringPoint;
    [SerializeField] private string displayName = "Ritual Bowl";

    public string Prompt(Carryable held, bool hasBook)
    {
        if (hasBook && !ritual.IsActive) return "[E] Begin the exorcism";
        return ritual.IsAwaiting && held is RitualItem item ? $"[E] Sacrifice the {item.DisplayName}" : displayName;
    }

    public bool Offer(RitualItem item)
    {
        SacrificeResult result = ritual.OfferSacrifice(item.ItemType);
        if (result == SacrificeResult.Refused) return false;
        item.Consume(offeringPoint.position);
        dissolve.Burn(item, result == SacrificeResult.Accepted);
        return true;
    }
}
