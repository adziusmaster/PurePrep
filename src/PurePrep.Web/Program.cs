using Microsoft.EntityFrameworkCore;
using PurePrep.Application;
using PurePrep.Domain;
using PurePrep.Infrastructure;
using PurePrep.Units;

var builder = WebApplication.CreateBuilder(args);

// --- Real PurePrep application services (shared with the MAUI app via PurePrep.Core) ---
var databasePath = Path.Combine(builder.Environment.ContentRootPath, "pureprep.web.db");
builder.Services.AddDbContextFactory<PurePrepDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddSingleton(_ =>
{
    var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    // Many recipe sites reject requests without a browser-like User-Agent (HTTP 403).
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
    return client;
});
builder.Services.AddSingleton<IRecipeParser, RecipeParser>();
builder.Services.AddSingleton<IRecipeRepository, SqliteRecipeRepository>();
builder.Services.AddSingleton<QuotaState>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// --- API: connects the browser preview to the real parser + repository ---
var api = app.MapGroup("/api");

api.MapGet("/state", async (string? units, IRecipeRepository repository, QuotaState quota) =>
{
    var display = ParseSystem(units);
    var recipes = await repository.GetAllAsync();
    return Results.Ok(new { recipes = recipes.Select(r => RecipeDto.From(r, display)), quota = quota.Snapshot(recipes.Count) });
});

api.MapPost("/parse", async (ParseRequest request, string? units, IRecipeParser parser, IRecipeRepository repository, QuotaState quota) =>
{
    var existing = await repository.GetAllAsync();
    if (!quota.CanSave(existing.Count))
        return Results.Ok(new { error = "quota", message = "Free limit reached. Upgrade to Premium for unlimited saves." });

    if (!Uri.TryCreate(request.Url?.Trim(), UriKind.Absolute, out var source) ||
        (source.Scheme != Uri.UriSchemeHttp && source.Scheme != Uri.UriSchemeHttps))
        return Results.Ok(new { error = "url", message = "Paste a valid http(s) recipe URL to begin." });

    try
    {
        var recipe = await parser.ParseAsync(source);
        await repository.SaveAsync(recipe);
        return Results.Ok(new { recipe = RecipeDto.From(recipe, ParseSystem(units)), quota = quota.Snapshot(existing.Count + 1) });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { error = "parse", message = $"Could not parse recipe: {ex.Message}" });
    }
});

api.MapPost("/recipe", async (RecipeInput input, string? units, IRecipeRepository repository, QuotaState quota) =>
{
    var existing = await repository.GetAllAsync();
    if (!quota.CanSave(existing.Count))
        return Results.Ok(new { error = "quota", message = "Free limit reached. Upgrade to Premium for unlimited saves." });

    var recipe = input.ToDomain();
    if (string.IsNullOrWhiteSpace(recipe.Title))
        return Results.Ok(new { error = "title", message = "Give your recipe a title." });

    await repository.SaveAsync(recipe);
    return Results.Ok(new { recipe = RecipeDto.From(recipe, ParseSystem(units)), quota = quota.Snapshot(existing.Count + 1) });
});

api.MapPut("/recipe/{id:guid}", async (Guid id, RecipeInput input, string? units, IRecipeRepository repository, QuotaState quota) =>
{
    var existing = await repository.GetAllAsync();
    var current = existing.FirstOrDefault(r => r.Id == id);
    if (current is null)
        return Results.Ok(new { error = "notfound", message = "That recipe no longer exists." });

    // The editor shows values in the user's chosen display system, so re-detect from what they saved.
    var recipe = input.ToDomain(id, current.SourceUrl, current.SavedAt);
    if (string.IsNullOrWhiteSpace(recipe.Title))
        return Results.Ok(new { error = "title", message = "Give your recipe a title." });

    await repository.UpdateAsync(recipe);
    return Results.Ok(new { recipe = RecipeDto.From(recipe, ParseSystem(units)), quota = quota.Snapshot(existing.Count) });
});

api.MapDelete("/recipe/{id:guid}", async (Guid id, IRecipeRepository repository, QuotaState quota) =>
{
    await repository.DeleteAsync(id);
    var count = (await repository.GetAllAsync()).Count;
    return Results.Ok(new { quota = quota.Snapshot(count) });
});

api.MapPost("/premium", async (PremiumRequest request, IRecipeRepository repository, QuotaState quota) =>
{
    quota.SetPremium(request.IsPremium);
    var count = (await repository.GetAllAsync()).Count;
    return Results.Ok(new { quota = quota.Snapshot(count) });
});

app.MapFallbackToFile("index.html");

app.Run();

static MeasurementSystem? ParseSystem(string? units) => units?.Trim().ToLowerInvariant() switch
{
    "metric" => MeasurementSystem.Metric,
    "imperial" => MeasurementSystem.Imperial,
    _ => null
};

// Singleton premium flag + quota derivation using the real domain rules (UserQuota.FreeRecipeLimit).
public sealed class QuotaState
{
    private bool _isPremium;

    public void SetPremium(bool isPremium) => _isPremium = isPremium;
    public bool CanSave(int savedCount) => _isPremium || savedCount < UserQuota.FreeRecipeLimit;

    public object Snapshot(int savedCount) => new
    {
        savedCount,
        isPremium = _isPremium,
        limit = UserQuota.FreeRecipeLimit,
        remaining = _isPremium ? -1 : Math.Max(0, UserQuota.FreeRecipeLimit - savedCount),
        canSave = CanSave(savedCount),
        canUseFocusMode = true
    };
}

public sealed record ParseRequest(string? Url);
public sealed record PremiumRequest(bool IsPremium);

public sealed record RecipeInput(string? Title, string[]? Ingredients, string[]? Steps)
{
    public ParsedRecipe ToDomain() => Build(Guid.NewGuid(), null, DateTimeOffset.UtcNow);
    public ParsedRecipe ToDomain(Guid id, string? sourceUrl, DateTimeOffset savedAt) => Build(id, sourceUrl, savedAt);

    private ParsedRecipe Build(Guid id, string? sourceUrl, DateTimeOffset savedAt)
    {
        var ingredients = (Ingredients ?? [])
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .ToArray();
        var steps = (Steps ?? [])
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .Select((text, index) => new RecipeStep { Order = index + 1, Instruction = text })
            .ToArray();
        return new()
        {
            Id = id,
            Title = (Title ?? string.Empty).Trim(),
            SourceUrl = sourceUrl,
            SavedAt = savedAt,
            Ingredients = ingredients,
            Steps = steps,
            SourceSystem = UnitConverter.Detect(ingredients.Concat(steps.Select(s => s.Instruction)))
        };
    }
}

public sealed record RecipeDto(Guid Id, string Title, string? SourceUrl, string SourceSystem, string DisplaySystem, IReadOnlyList<string> Ingredients, IReadOnlyList<string> Steps)
{
    public static RecipeDto From(ParsedRecipe r, MeasurementSystem? display = null)
    {
        var target = display ?? r.SourceSystem;
        var ingredients = UnitConverter.ConvertLines(r.Ingredients, r.SourceSystem, target);
        var steps = UnitConverter.ConvertLines(r.Steps.OrderBy(s => s.Order).Select(s => s.Instruction), r.SourceSystem, target);
        return new(r.Id, r.Title, r.SourceUrl, r.SourceSystem.ToString(), target.ToString(), ingredients, steps);
    }
}
