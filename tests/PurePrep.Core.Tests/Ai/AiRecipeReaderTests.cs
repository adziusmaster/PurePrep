using FluentAssertions;
using PurePrep.Ai;

namespace PurePrep.Core.Tests.Ai;

public sealed class AiRecipeReaderTests
{
    [Fact]
    public void Read_WhenPayloadIsV2_ShouldMapEverything()
    {
        // Arrange
        const string json = """
            {"title":" Pasta ","servings":4,"servingsNoun":"people","servingsEstimated":false,"prepMinutes":5,"cookMinutes":12,
             "ingredients":["200 g pasta","1 onion"],
             "steps":[{"text":"Fry the onion for 10-12 min.","ingredientRefs":[1],"timers":[{"label":"Fry onion","minSeconds":600,"maxSeconds":720}]},
                      {"text":"Boil the pasta.","ingredientRefs":[0],"timers":[]}]}
            """;

        // Act
        var recipe = AiRecipeReader.Read(json);

        // Assert
        recipe.Title.Should().Be("Pasta");
        recipe.Steps.Should().Equal("Fry the onion for 10-12 min.", "Boil the pasta.");
        recipe.Meta.Should().Be(new AiRecipeMeta(4, "people", false, 5, 12));
        recipe.StepDetails[0].Timers.Should().Equal(new AiTimer("Fry onion", 600, 720));
        recipe.StepDetails[0].IngredientRefs.Should().Equal(1);
    }

    [Fact]
    public void Read_WhenValuesAreOutOfRange_ShouldDropOnlyTheBadParts()
    {
        // Arrange
        const string json = """
            {"title":"X","servings":500,"prepMinutes":-3,"ingredients":["a","b"],
             "steps":[{"text":"Do it.","ingredientRefs":[0,7,-1,0],
                       "timers":[{"label":"","minSeconds":0,"maxSeconds":10},{"label":"Rest","minSeconds":900,"maxSeconds":600},{"label":"Long","minSeconds":1,"maxSeconds":999999}]},
                      {"ingredientRefs":[0]}]}
            """;

        // Act
        var recipe = AiRecipeReader.Read(json);

        // Assert
        recipe.Meta.Servings.Should().BeNull();
        recipe.Meta.PrepMinutes.Should().BeNull();
        recipe.Steps.Should().ContainSingle();
        recipe.StepDetails[0].IngredientRefs.Should().Equal(0);
        recipe.StepDetails[0].Timers.Should().Equal(new AiTimer("Rest", 600, 900));
    }

    [Fact]
    public void Read_WhenStepsAreLegacyStrings_ShouldStillReadThem()
    {
        // Arrange
        const string json = """{"title":"X","ingredients":["a"],"steps":["One.","Two."]}""";

        // Act
        var recipe = AiRecipeReader.Read(json);

        // Assert
        recipe.Steps.Should().Equal("One.", "Two.");
        recipe.StepDetails.Should().HaveCount(2);
        recipe.Meta.Should().Be(AiRecipeMeta.Empty);
    }
}
