using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

/// <summary>
/// The translation redesign keeps the original text pristine and caches paid translations per
/// language. These are the invariants the detail screen and persistence rely on.
/// </summary>
public sealed class ParsedRecipeTranslationTests
{
    private static ParsedRecipe Recipe() => new()
    {
        Title = "Lemon Pasta",
        SourceUrl = "https://example.com/pasta",
        Ingredients = ["200 g spaghetti", "1 lemon"],
        Steps =
        [
            new RecipeStep { Order = 1, Instruction = "Boil the pasta." },
            new RecipeStep { Order = 2, Instruction = "Toss together." },
        ],
        OriginalLanguage = "en",
    };

    private static RecipeTranslation German() => new()
    {
        Title = "Zitronen-Pasta",
        Ingredients = ["200 g Spaghetti", "1 Zitrone"],
        Steps =
        [
            new RecipeStep { Order = 1, Instruction = "Die Pasta kochen." },
            new RecipeStep { Order = 2, Instruction = "Alles vermengen." },
        ],
    };

    [Fact]
    public void Displayed_WithNoTranslationSelected_ShouldReturnTheOriginal()
    {
        var recipe = Recipe();

        recipe.Displayed().Title.Should().Be("Lemon Pasta");
    }

    [Fact]
    public void WithTranslation_ShouldCacheItAndDisplayIt_WhileKeepingTheOriginalPristine()
    {
        // Act
        var translated = Recipe().WithTranslation("de", German());

        // Assert — the pristine fields never change; only the projection does.
        translated.Title.Should().Be("Lemon Pasta");
        translated.HasTranslation("de").Should().BeTrue();
        translated.DisplayLanguage.Should().Be("de");

        var shown = translated.Displayed();
        shown.Title.Should().Be("Zitronen-Pasta");
        shown.Ingredients.Should().Contain("1 Zitrone");
        shown.Steps.Select(s => s.Instruction).Should().Contain("Alles vermengen.");
    }

    [Fact]
    public void SwitchingBackToOriginal_ShouldBeLosslessAndKeepTheCache()
    {
        var translated = Recipe().WithTranslation("de", German());

        var backToOriginal = translated.WithDisplayLanguage(null);

        backToOriginal.Displayed().Title.Should().Be("Lemon Pasta");
        // Free re-switch: the paid translation is still cached.
        backToOriginal.HasTranslation("de").Should().BeTrue();
    }

    [Fact]
    public void Backup_ShouldPreservePaidTranslations_AndResetToOriginalOnRestore()
    {
        var translated = Recipe().WithTranslation("de", German());

        var restored = RecipeBackup.Import(RecipeBackup.Export([translated])).Single();

        restored.HasTranslation("de").Should().BeTrue("a paid translation must survive a backup");
        restored.Displayed().Title.Should().Be("Lemon Pasta", "a restore should show the original");
        restored.Translations["de"].Title.Should().Be("Zitronen-Pasta");
    }
}
