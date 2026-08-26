namespace PurePrep.Server.Endpoints;

// --- AI parse ---
public sealed record ParseRequest(Guid DeviceId, string Url, string? Language = null);

public sealed record RecipeResponse(
    string Title,
    string? SourceUrl,
    string SourceSystem,
    IReadOnlyList<string> Ingredients,
    IReadOnlyList<string> Steps);

public sealed record ParseResponse(RecipeResponse Recipe, int RemainingCredits);

// --- Billing ---
public sealed record RedeemRequest(Guid DeviceId, string ProductId, string PurchaseToken);
public sealed record RedeemResponse(int CreditsGranted, int Balance);

// --- Promo codes ---
public sealed record RedeemCodeRequest(Guid DeviceId, string Code);
public sealed record RedeemCodeResponse(int CreditsGranted, int Balance);
public sealed record CreatePromoRequest(string? Code, int? Credits, int? ExpiresInDays);
public sealed record PromoResponse(
    string Code,
    int Credits,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool Revoked,
    int RedemptionCount);

// --- Dev grant ---
public sealed record GrantRequest(Guid DeviceId, int Amount);
