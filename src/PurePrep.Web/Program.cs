using Microsoft.EntityFrameworkCore;
using PurePrep.Ai;
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
builder.Services.AddSingleton<IRecipeRepository, SqliteRecipeRepository>();
builder.Services.AddSingleton<CreditState>();

// --- AI Smart Parser (Gemini) + SSRF guard, shared with the backend via PurePrep.Core ---
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.AddSingleton<IUrlGuard, UrlGuard>();
var geminiKey = builder.Configuration[$"{GeminiOptions.SectionName}:ApiKey"];
if (!string.IsNullOrWhiteSpace(geminiKey))
    builder.Services.AddHttpClient<IGeminiClient, GeminiClient>(c =>
        c.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));
else
    builder.Services.AddSingleton<IGeminiClient, FakeGeminiClient>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Clean URL for the Play Store-required privacy policy (also reachable at /privacy.html).
app.MapGet("/privacy", (IWebHostEnvironment env) =>
    Results.File(Path.Combine(env.WebRootPath, "privacy.html"), "text/html; charset=utf-8"));

// --- API: connects the browser preview to the real parser + repository ---
var api = app.MapGroup("/api");

api.MapGet("/state", async (string? units, IRecipeRepository repository, CreditState credits) =>
{
    var display = ParseSystem(units);
    var recipes = await repository.GetAllAsync();
    return Results.Ok(new { recipes = recipes.Select(r => RecipeDto.From(r, display)), credits = credits.Snapshot() });
});

// Link import is powered by the AI Smart Parser and consumes one credit. When credits run out the
// import is disabled (402); manual add always remains available.
api.MapPost("/parse", async (ParseRequest request, string? units,
    IRecipeRepository repository, CreditState credits, IUrlGuard urlGuard,
    IGeminiClient gemini, HttpClient http) =>
{
    if (!Uri.TryCreate(request.Url?.Trim(), UriKind.Absolute, out var source) ||
        (source.Scheme != Uri.UriSchemeHttp && source.Scheme != Uri.UriSchemeHttps))
        return Results.Ok(new { error = "url", message = "Paste a valid http(s) recipe URL to begin." });

    if (!await urlGuard.IsPublicHttpAsync(source))
        return Results.Ok(new { error = "url", message = "That URL is not an allowed public address." });

    if (!credits.TrySpend())
        return Results.Ok(new { error = "credits", message = "You're out of Smart Credits. Add a recipe manually, or top up to import by link.", credits = credits.Snapshot() });

    try
    {
        using var page = await http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead);
        page.EnsureSuccessStatusCode();
        var text = PageText.Extract(await page.Content.ReadAsStringAsync());
        var ai = await gemini.ExtractAsync(text);

        var system = UnitConverter.Detect(ai.Ingredients.Concat(ai.Steps));
        var recipe = new ParsedRecipe
        {
            Title = ai.Title,
            SourceUrl = source.ToString(),
            Ingredients = ai.Ingredients,
            Steps = ai.Steps.Select((t, i) => new RecipeStep { Order = i + 1, Instruction = t }).ToArray(),
            SourceSystem = system,
        };
        await repository.SaveAsync(recipe);
        return Results.Ok(new { recipe = RecipeDto.From(recipe, ParseSystem(units)), credits = credits.Snapshot() });
    }
    catch (Exception ex)
    {
        credits.Refund(); // never charge for a failed import
        return Results.Ok(new { error = "parse", message = $"Could not extract a recipe from this page: {ex.Message}", credits = credits.Snapshot() });
    }
});

api.MapPost("/recipe", async (RecipeInput input, string? units, IRecipeRepository repository, CreditState credits) =>
{
    var recipe = input.ToDomain();
    if (string.IsNullOrWhiteSpace(recipe.Title))
        return Results.Ok(new { error = "title", message = "Give your recipe a title." });

    await repository.SaveAsync(recipe);
    return Results.Ok(new { recipe = RecipeDto.From(recipe, ParseSystem(units)), credits = credits.Snapshot() });
});

api.MapPut("/recipe/{id:guid}", async (Guid id, RecipeInput input, string? units, IRecipeRepository repository, CreditState credits) =>
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
    return Results.Ok(new { recipe = RecipeDto.From(recipe, ParseSystem(units)), credits = credits.Snapshot() });
});

api.MapDelete("/recipe/{id:guid}", async (Guid id, IRecipeRepository repository, CreditState credits) =>
{
    await repository.DeleteAsync(id);
    return Results.Ok(new { credits = credits.Snapshot() });
});

// Demo top-up standing in for a Google Play "Smart Credit Pack" purchase.
api.MapPost("/credits/grant", (GrantInput input, CreditState credits) =>
{
    credits.Grant(input.Amount > 0 ? input.Amount : 10);
    return Results.Ok(new { credits = credits.Snapshot() });
});

app.MapFallbackToFile("index.html");

app.Run();

static MeasurementSystem? ParseSystem(string? units) => units?.Trim().ToLowerInvariant() switch
{
    "metric" => MeasurementSystem.Metric,
    "imperial" => MeasurementSystem.Imperial,
    _ => null
};

// In-memory Smart Credit balance for the browser preview. Every visitor starts with the free
// allowance (UserQuota.FreeCredits). Resets when the preview server restarts.
public sealed class CreditState
{
    private int _balance = UserQuota.FreeCredits;

    public bool TrySpend()
    {
        if (_balance <= 0) return false;
        _balance--;
        return true;
    }

    public void Refund() => _balance++;
    public void Grant(int amount) => _balance += amount;

    public object Snapshot() => new
    {
        balance = _balance,
        freeAllowance = UserQuota.FreeCredits,
        canImport = _balance > 0
    };
}

public sealed record ParseRequest(string? Url);
public sealed record GrantInput(int Amount);

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
