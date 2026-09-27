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

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("intent://evil#Intent;end")]
    [InlineData("not a url")]
    public void Import_ShouldDropASourceUrlThatIsNotAnHttpLink(string sourceUrl)
    {
        // Arrange — a backup file is user-editable, so a stored URL is untrusted and must not be
        // able to smuggle a non-web scheme through to a link launcher.
        var json = $$"""
            {"version":1,"recipes":[
              {"id":"11111111-1111-1111-1111-111111111111","title":"Crafted","sourceUrl":{{System.Text.Json.JsonSerializer.Serialize(sourceUrl)}},"ingredients":["a"],"steps":["Do it."],"sourceSystem":"Metric"}
            ]}
            """;

        // Act
        var restored = RecipeBackup.Import(json).Single();

        // Assert
        restored.SourceUrl.Should().BeNull();
    }

    [Fact]
    public void Import_ShouldKeepAnHttpSourceUrl()
    {
        // Arrange
        var json = """
            {"version":1,"recipes":[
              {"id":"11111111-1111-1111-1111-111111111111","title":"Good","sourceUrl":"https://example.com/pasta","ingredients":["a"],"steps":["Do it."],"sourceSystem":"Metric"}
            ]}
            """;

        // Act
        var restored = RecipeBackup.Import(json).Single();

        // Assert
        restored.SourceUrl.Should().Be("https://example.com/pasta");
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

    [Fact]
    public void ImportWithImages_WhenExportedWithV2Fields_ShouldRoundTripEverything()
    {
        // Arrange
        var recipe = new ParsedRecipe
        {
            Title = "Pasta",
            Ingredients = ["200 g pasta"],
            Steps = [new RecipeStep { Order = 1, Instruction = "Boil.", Timers = [new RecipeTimer("Boil", 600, 720)], IngredientRefs = [0] }],
            Servings = 4, ServingsNoun = "people", PrepMinutes = 5, CookMinutes = 12,
            Notes = "Less salt", IsFavourite = true, ImagePath = "p.jpg",
        }.MarkCooked(DateTimeOffset.UnixEpoch);
        var images = new Dictionary<Guid, byte[]> { [recipe.Id] = [1, 2, 3] };

        // Act
        var contents = RecipeBackup.ImportWithImages(RecipeBackup.Export([recipe], images));

        // Assert
        var restored = contents.Recipes.Should().ContainSingle().Subject;
        restored.Steps[0].Timers.Should().Equal(new RecipeTimer("Boil", 600, 720));
        restored.Steps[0].IngredientRefs.Should().Equal(0);
        restored.Notes.Should().Be("Less salt");
        restored.Status.Should().Be(RecipeStatus.Cooked);
        restored.CookCount.Should().Be(1);
        restored.ImagePath.Should().BeNull("the image path is re-assigned when the restored bytes are saved");
        contents.Images[recipe.Id].Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Import_WhenFileIsV1_ShouldStillRestoreWithDefaults()
    {
        // Arrange
        const string v1 = """
            {"version":1,"exportedAt":"2026-01-01T00:00:00Z","recipes":[
              {"id":"7d1f2a3b-0000-0000-0000-000000000001","title":"Old","sourceSystem":"Metric",
               "savedAt":"2026-01-01T00:00:00Z","ingredients":["1 egg"],"steps":["Boil 5 min."]}]}
            """;

        // Act
        var recipes = RecipeBackup.Import(v1);

        // Assert
        var old = recipes.Should().ContainSingle().Subject;
        old.Status.Should().Be(RecipeStatus.WantToCook);
        old.Steps[0].Instruction.Should().Be("Boil 5 min.");
        old.Steps[0].Timers.Should().BeEmpty();
    }

    [Fact]
    public void ImportWithImages_WhenImageBase64IsCorrupt_ShouldSkipOnlyTheImage()
    {
        // Arrange
        const string json = """
            {"version":2,"exportedAt":"2026-01-01T00:00:00Z","recipes":[
              {"id":"7d1f2a3b-0000-0000-0000-000000000001","title":"X","sourceSystem":"Metric",
               "savedAt":"2026-01-01T00:00:00Z","ingredients":["1 egg"],"steps":["Boil."],"imageBase64":"%%%"}]}
            """;

        // Act
        var contents = RecipeBackup.ImportWithImages(json);

        // Assert
        contents.Recipes.Should().ContainSingle();
        contents.Images.Should().BeEmpty();
    }

    [Fact]
    public void ImportWithImages_WhenRecipeHasNoId_ShouldKeyTheImageToTheSameGeneratedId()
    {
        // Arrange — a hand-edited backup can omit (or zero out) the id; the restored recipe and its
        // image must still end up sharing one generated id rather than two different ones.
        const string json = """
            {"version":2,"exportedAt":"2026-01-01T00:00:00Z","recipes":[
              {"id":"00000000-0000-0000-0000-000000000000","title":"No Id","sourceSystem":"Metric",
               "savedAt":"2026-01-01T00:00:00Z","ingredients":["1 egg"],"steps":["Boil."],"imageBase64":"AQID"}]}
            """;

        // Act
        var contents = RecipeBackup.ImportWithImages(json);

        // Assert
        var restored = contents.Recipes.Should().ContainSingle().Subject;
        contents.Images.Should().ContainKey(restored.Id);
        contents.Images[restored.Id].Should().Equal(1, 2, 3);
    }
}
