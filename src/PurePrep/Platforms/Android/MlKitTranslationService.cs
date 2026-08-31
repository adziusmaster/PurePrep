using Android.Gms.Extensions;
using Android.Runtime;
using Java.Util;
using PurePrep.Application;
using PurePrep.Domain;
using Xamarin.Google.MLKit.Common.Model;
using Xamarin.Google.MLKit.NL.Translate;

namespace PurePrep.Platforms.Android;

/// <summary>
/// On-device recipe translation using Google ML Kit Translate. Language models are
/// downloaded once (~30 MB each) and then run fully offline — no credits, no network per use.
/// English is ML Kit's bundled pivot language and is always available.
/// </summary>
public sealed class MlKitTranslationService : ITranslationService
{
    private static readonly string[] Codes = { "en", "de", "fr", "es", "it", "pl", "nl" };

    public bool IsSupported => true;

    public IReadOnlyList<string> SupportedLanguageCodes => Codes;

    private static string RequireMlKitCode(string iso)
    {
        var code = TranslateLanguage.FromLanguageTag(iso);
        if (string.IsNullOrEmpty(code))
            throw new NotSupportedException($"ML Kit does not support language '{iso}'.");
        return code!;
    }

    private static DownloadConditions Conditions(bool requireWifi)
    {
        var builder = new DownloadConditions.Builder();
        if (requireWifi)
            builder.RequireWifi();
        return builder.Build();
    }

    private static TranslateRemoteModel ModelFor(string iso) =>
        new TranslateRemoteModel.Builder(RequireMlKitCode(iso)).Build();

    public async Task<bool> IsModelDownloadedAsync(string code, CancellationToken ct = default)
    {
        // English ships bundled with ML Kit and is always present.
        if (string.Equals(code, "en", StringComparison.OrdinalIgnoreCase))
            return true;

        var result = await RemoteModelManager.Instance
            .IsModelDownloaded(ModelFor(code))
            .AsAsync<Java.Lang.Boolean>();
        return result?.BooleanValue() ?? false;
    }

    public async Task<IReadOnlyList<string>> GetDownloadedModelsAsync(CancellationToken ct = default)
    {
        var cls = Java.Lang.Class.FromType(typeof(TranslateRemoteModel));
        var obj = await RemoteModelManager.Instance.GetDownloadedModels(cls).AsAsync<Java.Lang.Object>();

        var codes = new List<string> { "en" };
        if (obj is not null)
        {
            var set = obj.JavaCast<ISet>();
            foreach (var item in set.ToArray())
            {
                if (item is null) continue;
                var model = item.JavaCast<TranslateRemoteModel>();
                var lang = model.Language;
                if (!string.IsNullOrEmpty(lang) && !codes.Contains(lang))
                    codes.Add(lang!);
            }
        }
        return codes;
    }

    public async Task DownloadModelAsync(string code, bool requireWifi = true, CancellationToken ct = default)
    {
        if (string.Equals(code, "en", StringComparison.OrdinalIgnoreCase))
            return; // bundled

        await RemoteModelManager.Instance
            .Download(ModelFor(code), Conditions(requireWifi))
            .AsAsync();
    }

    public async Task DeleteModelAsync(string code, CancellationToken ct = default)
    {
        if (string.Equals(code, "en", StringComparison.OrdinalIgnoreCase))
            return; // cannot delete the bundled pivot model

        await RemoteModelManager.Instance
            .DeleteDownloadedModel(ModelFor(code))
            .AsAsync();
    }

    public Task<string?> DetectLanguageAsync(string text, CancellationToken ct = default) =>
        Task.FromResult(LanguageHeuristics.Detect(text));

    public async Task<ParsedRecipe> TranslateAsync(ParsedRecipe recipe, string targetCode, bool requireWifi = true, CancellationToken ct = default)
    {
        var combined = string.Join("\n", new[] { recipe.Title }
            .Concat(recipe.Ingredients)
            .Concat(recipe.Steps.Select(s => s.Instruction)));

        // Only skip translation when we CONFIDENTLY detect the recipe is already in the target
        // language. A null detection means "unknown source" (e.g. an import in a language our
        // heuristics don't cover) — previously we defaulted that to "en" and wrongly short-circuited
        // when the user's target was English, leaving the recipe untranslated. Fall back to "en" as
        // the ML Kit source only for the actual translation attempt.
        var detected = LanguageHeuristics.Detect(combined);
        if (detected is not null && string.Equals(detected, targetCode, StringComparison.OrdinalIgnoreCase))
            return recipe; // already in the requested language

        var source = detected ?? "en";

        var options = new TranslatorOptions.Builder()
            .SetSourceLanguage(RequireMlKitCode(source))
            .SetTargetLanguage(RequireMlKitCode(targetCode))
            .Build();

        var translator = Translation.GetClient(options);
        try
        {
            await translator.DownloadModelIfNeeded(Conditions(requireWifi)).AsAsync();

            var title = await TranslateLineAsync(translator, recipe.Title, ct);
            var ingredients = new List<string>(recipe.Ingredients.Count);
            foreach (var line in recipe.Ingredients)
                ingredients.Add(await TranslateLineAsync(translator, line, ct));

            var steps = new List<RecipeStep>(recipe.Steps.Count);
            foreach (var step in recipe.Steps)
                steps.Add(new RecipeStep
                {
                    Order = step.Order,
                    Instruction = await TranslateLineAsync(translator, step.Instruction, ct),
                });

            return new ParsedRecipe
            {
                Id = recipe.Id,
                Title = title,
                SourceUrl = recipe.SourceUrl,
                Ingredients = ingredients,
                Steps = steps,
                SourceSystem = recipe.SourceSystem,
                SavedAt = recipe.SavedAt,
            };
        }
        finally
        {
            translator.Close();
        }
    }

    private static async Task<string> TranslateLineAsync(ITranslator translator, string line, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        ct.ThrowIfCancellationRequested();
        var result = await translator.Translate(line).AsAsync<Java.Lang.Object>();
        return result?.ToString() ?? line;
    }
}
