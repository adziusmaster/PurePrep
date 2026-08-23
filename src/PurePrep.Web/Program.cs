using Microsoft.EntityFrameworkCore;
using PurePrep.Application;
using PurePrep.Domain;
using PurePrep.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- Real PurePrep application services (shared with the MAUI app via PurePrep.Core) ---
var databasePath = Path.Combine(builder.Environment.ContentRootPath, "pureprep.web.db");
builder.Services.AddDbContextFactory<PurePrepDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(20) });
builder.Services.AddSingleton<IRecipeParser, RecipeParser>();
builder.Services.AddSingleton<IRecipeRepository, SqliteRecipeRepository>();
builder.Services.AddSingleton<QuotaState>();

var app = builder.Build();

// Ensure the database exists and seed a few sample recipes on first run so the
// browser preview shows content, just like the MAUI app's starter library.
await using (var scope = app.Services.CreateAsyncScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IRecipeRepository>();
    var existing = await repository.GetAllAsync();
    if (existing.Count == 0)
    {
        foreach (var recipe in SampleRecipes.Create())
            await repository.SaveAsync(recipe);
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();

// --- API: connects the browser preview to the real parser + repository ---
var api = app.MapGroup("/api");

api.MapGet("/state", async (IRecipeRepository repository, QuotaState quota) =>
{
    var recipes = await repository.GetAllAsync();
    return Results.Ok(new { recipes = recipes.Select(RecipeDto.From), quota = quota.Snapshot(recipes.Count) });
});

api.MapPost("/parse", async (ParseRequest request, IRecipeParser parser, IRecipeRepository repository, QuotaState quota) =>
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
        return Results.Ok(new { recipe = RecipeDto.From(recipe), quota = quota.Snapshot(existing.Count + 1) });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { error = "parse", message = $"Could not parse recipe: {ex.Message}" });
    }
});

api.MapPost("/premium", async (PremiumRequest request, IRecipeRepository repository, QuotaState quota) =>
{
    quota.SetPremium(request.IsPremium);
    var count = (await repository.GetAllAsync()).Count;
    return Results.Ok(new { quota = quota.Snapshot(count) });
});

app.MapFallbackToFile("index.html");

app.Run();

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
        canUseFocusMode = _isPremium
    };
}

public sealed record ParseRequest(string? Url);
public sealed record PremiumRequest(bool IsPremium);

public sealed record RecipeDto(Guid Id, string Title, string? SourceUrl, IReadOnlyList<string> Ingredients, IReadOnlyList<string> Steps)
{
    public static RecipeDto From(ParsedRecipe r) =>
        new(r.Id, r.Title, r.SourceUrl, r.Ingredients, r.Steps.OrderBy(s => s.Order).Select(s => s.Instruction).ToArray());
}

public static class SampleRecipes
{
    public static IEnumerable<ParsedRecipe> Create() =>
    [
        new()
        {
            Title = "Miso butter mushrooms",
            SourceUrl = "https://pureprep.local/recipes/miso-mushrooms",
            Ingredients = ["450 g mushrooms", "2 tbsp butter", "1 tbsp white miso", "1 tsp sesame oil"],
            Steps =
            [
                new RecipeStep { Order = 1, Instruction = "Wipe the mushrooms clean and tear any large ones in half." },
                new RecipeStep { Order = 2, Instruction = "Sear in a hot pan until deeply golden, 6 to 8 minutes." },
                new RecipeStep { Order = 3, Instruction = "Lower the heat. Add butter, miso, and sesame oil, then toss." }
            ]
        },
        new()
        {
            Title = "Weeknight tomato orzo",
            SourceUrl = "https://pureprep.local/recipes/tomato-orzo",
            Ingredients = ["250 g orzo", "400 g chopped tomatoes", "700 ml vegetable stock", "1 lemon"],
            Steps =
            [
                new RecipeStep { Order = 1, Instruction = "Toast the orzo in olive oil for 2 minutes." },
                new RecipeStep { Order = 2, Instruction = "Stir in tomatoes and stock. Simmer until tender." },
                new RecipeStep { Order = 3, Instruction = "Finish with lemon zest, juice, and black pepper." }
            ]
        },
        new()
        {
            Title = "Crisp-edged potato frittata",
            SourceUrl = "https://pureprep.local/recipes/potato-frittata",
            Ingredients = ["500 g potatoes", "6 eggs", "1 small onion", "80 g cheddar"],
            Steps =
            [
                new RecipeStep { Order = 1, Instruction = "Boil sliced potatoes until just tender, then drain." },
                new RecipeStep { Order = 2, Instruction = "Soften the onion in an oven-safe skillet." },
                new RecipeStep { Order = 3, Instruction = "Add potatoes and beaten eggs. Cook until set around the edge." }
            ]
        }
    ];
}
