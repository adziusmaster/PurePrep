using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

/// <summary>
/// Recipes live only in one local SQLite file. Losing the phone, or uninstalling, loses the whole
/// library — including recipes that cost Smart Credits to import. These are the round-trip
/// guarantees the backup format has to hold.
/// </summary>
public sealed class RecipeBackupTests
{
    private static ParsedRecipe Recipe(string title = "Lemon Pasta") => new()
    {
        Title = title,
        SourceUrl = "https://example.com/pasta",
        SourceSystem = MeasurementSystem.Metric,
        Ingredients = ["200 g spaghetti", "1 lemon"],
        Steps =
        [
            new RecipeStep { Order = 1, Instruction = "Boil the pasta." },
            new RecipeStep { Order = 2, Instruction = "Toss together." },
        ],
    };

    [Fact]
    public void Export_ThenImport_ShouldPreserveEveryField()
    {
        // Arrange
        var original = Recipe();

        // Act
        var restored = RecipeBackup.Import(RecipeBackup.Export([original])).Single();

        // Assert
        restored.Id.Should().Be(original.Id);
        restored.Title.Should().Be(original.Title);
        restored.SourceUrl.Should().Be(original.SourceUrl);
        restored.SourceSystem.Should().Be(original.SourceSystem);
        restored.Ingredients.Should().Equal(original.Ingredients);
        restored.Steps.Select(s => s.Instruction).Should().Equal("Boil the pasta.", "Toss together.");
        restored.SavedAt.Should().BeCloseTo(original.SavedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Export_ThenImport_ShouldPreserveEveryRecipe()
    {
        // Arrange
        var recipes = new[] { Recipe("One"), Recipe("Two"), Recipe("Three") };

        // Act
        var restored = RecipeBackup.Import(RecipeBackup.Export(recipes));

        // Assert
        restored.Select(r => r.Title).Should().Equal("One", "Two", "Three");
    }

    [Fact]
    public void Export_ShouldRecordAFormatVersion()
    {
        // Arrange — a version lets a future format change still read today's backups.
        // Act
        var json = RecipeBackup.Export([Recipe()]);

        // Assert
        json.Should().Contain("\"version\"");
    }

    [Fact]
    public void Export_OfAnEmptyLibrary_ShouldStillProduceValidJson()
    {
        // Act
        var restored = RecipeBackup.Import(RecipeBackup.Export([]));

        // Assert
        restored.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Import_OfSomethingThatIsNotABackup_ShouldThrowAClearError(string json)
    {
        // Arrange — users will pick the wrong file; it must fail with an explanation, not a crash.
        var act = () => RecipeBackup.Import(json);

        // Assert
        act.Should().Throw<InvalidBackupException>();
    }

    [Fact]
    public void Import_ShouldSkipEntriesWithNoTitle()
    {
        // Arrange — a half-written file should yield what is readable rather than nothing.
        var json = """
            {"version":1,"recipes":[
              {"id":"11111111-1111-1111-1111-111111111111","title":"Good","ingredients":["a"],"steps":["Do it."],"sourceSystem":"Metric"},
              {"id":"22222222-2222-2222-2222-222222222222","title":"","ingredients":[],"steps":[]}
            ]}
            """;

        // Act
        var restored = RecipeBackup.Import(json);

        // Assert
        restored.Should().ContainSingle().Which.Title.Should().Be("Good");
    }

    [Fact]
    public void ToPlainText_ShouldProduceSomethingReadableWhenPastedIntoAMessage()
    {
        // Act
        var text = RecipeBackup.ToPlainText(Recipe());

        // Assert
        text.Should().Contain("Lemon Pasta")
            .And.Contain("200 g spaghetti")
            .And.Contain("1. Boil the pasta.")
            .And.Contain("2. Toss together.")
            .And.Contain("https://example.com/pasta");
    }

    [Fact]
    public void ToPlainText_WhenThereIsNoSource_ShouldNotLeaveADanglingLabel()
    {
        // Arrange
        var manual = new ParsedRecipe { Title = "Nan's Scones", Ingredients = ["flour"], Steps = [] };

        // Act
        var text = RecipeBackup.ToPlainText(manual);

        // Assert
        text.Should().Contain("Nan's Scones");
        text.Should().NotContain("http");
    }
}
