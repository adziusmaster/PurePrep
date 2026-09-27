using System.Net;
using FluentAssertions;
using NSubstitute;
using PurePrep.Application;
using PurePrep.Core.Tests.TestSupport;
using PurePrep.Domain;
using PurePrep.Infrastructure;

namespace PurePrep.Core.Tests.Infrastructure;

public sealed class AiProxyRecipeParserTests
{
    private static AiProxyRecipeParser Sut(string json)
    {
        var identity = Substitute.For<IDeviceIdentity>();
        identity.GetDeviceIdAsync(Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
        var http = new HttpClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json)) { BaseAddress = new Uri("https://api.test/") };
        return new AiProxyRecipeParser(http, identity);
    }

    [Fact]
    public async Task ParseAsync_WhenResponseIsV2_ShouldMapStructureAndImage()
    {
        // Arrange
        var sut = Sut("""
            {"recipe":{"title":"Pasta","sourceUrl":"https://a.b/p","sourceSystem":"Metric","ingredients":["200 g pasta"],
             "steps":["Boil 10 min."],"servings":4,"servingsNoun":null,"servingsEstimated":true,"prepMinutes":5,"cookMinutes":10,
             "imageUrl":"https://a.b/p.jpg","imageTicket":null,
             "stepDetails":[{"text":"Boil 10 min.","ingredientRefs":[0],"timers":[{"label":"Boil pasta","minSeconds":600,"maxSeconds":600}]}]},
             "remainingCredits":3}
            """);

        // Act
        var import = await sut.ParseAsync(new Uri("https://a.b/p"));

        // Assert
        import.ImageUrl.Should().Be(new Uri("https://a.b/p.jpg"));
        import.Recipe.Servings.Should().Be(4);
        import.Recipe.ServingsEstimated.Should().BeTrue();
        import.Recipe.Steps[0].Timers.Should().Equal(new RecipeTimer("Boil pasta", 600, 600));
        import.Recipe.Steps[0].IngredientRefs.Should().Equal(0);
    }

    [Fact]
    public async Task ParseAsync_WhenResponseIsLegacy_ShouldStillMapSteps()
    {
        // Arrange
        var sut = Sut("""{"recipe":{"title":"Pasta","sourceSystem":"Metric","ingredients":["a"],"steps":["One.","Two."]},"remainingCredits":3}""");

        // Act
        var import = await sut.ParseAsync(new Uri("https://a.b/p"));

        // Assert
        import.Recipe.Steps.Select(s => s.Instruction).Should().Equal("One.", "Two.");
        import.ImageUrl.Should().BeNull();
        import.ImageTicket.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_WhenStepDetailsCountMismatches_ShouldFallBackToPlainSteps()
    {
        // Arrange
        var sut = Sut("""{"recipe":{"title":"P","sourceSystem":"Metric","ingredients":["a"],"steps":["One.","Two."],"stepDetails":[{"text":"One.","ingredientRefs":[],"timers":[]}]},"remainingCredits":3}""");

        // Act
        var import = await sut.ParseAsync(new Uri("https://a.b/p"));

        // Assert
        import.Recipe.Steps.Should().HaveCount(2);
        import.Recipe.Steps.Should().OnlyContain(s => s.Timers.Count == 0);
    }
}
