namespace PurePrep.Server.Endpoints;

// --- AI parse ---
public sealed record ParseRequest(Guid DeviceId, string Url);

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

// --- Dev grant ---
public sealed record GrantRequest(Guid DeviceId, int Amount);
