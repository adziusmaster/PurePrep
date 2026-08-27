namespace PurePrep.Server.Services;

/// <summary>
/// Monetization configuration. Credit packs are defined here (and overridable via appsettings /
/// environment) so pricing/pack layout can change without touching business logic.
/// Each pack maps a Google Play consumable product id to the number of credits it grants.
/// </summary>
public sealed class CreditOptions
{
    public const string SectionName = "Credits";

    /// <summary>Credit packs keyed by Google Play product id. Defaults: 10 / 20 / 50 / 150.</summary>
    public Dictionary<string, int> Packs { get; set; } = new()
    {
        ["credits_10"] = 10,
        ["credits_20"] = 20,
        ["credits_50"] = 50,
        ["credits_150"] = 150,
    };

    /// <summary>Credits charged per AI parse. Kept configurable for future tuning.</summary>
    public int CostPerParse { get; set; } = 1;

    /// <summary>Free AI Smart Credits seeded to every new device on first contact.</summary>
    public int FreeCredits { get; set; } = 10;

    /// <summary>
    /// How many brand-new devices a single origin may be granted free credits for per day.
    /// Device ids are client-generated, so without this a script mints unlimited free parses.
    /// Set generously enough for a shared household or office NAT.
    /// </summary>
    public int MaxNewDevicesPerIpPerDay { get; set; } = 5;

    /// <summary>Window the above cap is measured over.</summary>
    public TimeSpan NewDeviceWindow { get; set; } = TimeSpan.FromDays(1);
}
