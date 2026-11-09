using System;

[Flags]
public enum EvidenceType
{
    None = 0,
    Emf5 = 1,
    Freezing = 2,
    Evp = 4,
    UvPrints = 8,
    RemPod = 16,
    GhostOrb = 32
}

public static class EvidenceNames
{
    public static readonly EvidenceType[] All =
    {
        EvidenceType.Emf5,
        EvidenceType.Freezing,
        EvidenceType.UvPrints,
        EvidenceType.RemPod,
        EvidenceType.GhostOrb,
        EvidenceType.Evp
    };

    public static string Get(EvidenceType evidence)
    {
        return evidence switch
        {
            EvidenceType.Emf5 => "EMF Level 5",
            EvidenceType.Freezing => "Freezing Temperatures",
            EvidenceType.UvPrints => "UV Prints",
            EvidenceType.RemPod => "REM Pod",
            EvidenceType.GhostOrb => "Ghost Orb",
            EvidenceType.Evp => "EVP Recording",
            _ => evidence.ToString()
        };
    }
}