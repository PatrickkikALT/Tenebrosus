public enum RitualItemType
{
    Salt,
    Candle,
    HolyWater,
    Sage,
    Bell,
    Crucifix,
    IronNail,
    Coin
}

public static class RitualItemNames
{
    public static string Get(RitualItemType item)
    {
        return item switch
        {
            RitualItemType.HolyWater => "Holy Water",
            RitualItemType.IronNail => "Iron Nail",
            _ => item.ToString()
        };
    }
}