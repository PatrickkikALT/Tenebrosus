public enum RitualItemType
{
    Salt,
    Candle,
    HolyWater,
    Sage,
    Bell,
    Crucifix
}

public static class RitualItemNames
{
    public static string Get(RitualItemType item)
    {
        return item switch
        {
            RitualItemType.HolyWater => "Holy Water",
            _ => item.ToString()
        };
    }
}