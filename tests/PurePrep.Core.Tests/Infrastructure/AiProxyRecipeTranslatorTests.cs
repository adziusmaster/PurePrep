using System.Net;
using FluentAssertions;
using NSubstitute;
using PurePrep.Application;
using PurePrep.Core.Tests.TestSupport;
using PurePrep.Domain;
using PurePrep.Infrastructure;

namespace PurePrep.Core.Tests.Infrastructure;

public sealed class AiProxyRecipeTranslatorTests
{
    private static ParsedRecipe Recipe() => new()
    {
        Title = "Pasta",
        Ingredients = ["onion"],
        Steps = [new RecipeStep { Order = 1, Instruction = "Fry the onion.", IngredientRefs = [0], Timers = [new RecipeTimer("Fry onion", 600, 720)] }],
    };

    private static IDeviceIdentity Identity()
    {
        var identity = Substitute.For<IDeviceIdentity>();
        identity.GetDeviceIdAsync(Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
        return identity;
    }

    [Fact]
    public async Task TranslateAsync_WhenLabelsReturned_ShouldKeepDurationsAndRefs()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"recipe":{"title":"Makaron","sourceSystem":"Metric","ingredients":["cebula"],"steps":["Smaż cebulę."],"timerLabels":["Smaż cebulę"]},"remainingCredits":5}""");
        var sut = new AiProxyRecipeTranslator(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") }, Identity());

        // Act
        var translation = await sut.TranslateAsync(Recipe(), "pl");

        // Assert
        var step = translation.Steps.Single();
        step.Timers.Should().Equal(new RecipeTimer("Smaż cebulę", 600, 720));
        step.IngredientRefs.Should().Equal(0);
    }

    [Fact]
    public async Task TranslateAsync_WhenLabelCountDiffers_ShouldKeepOriginalLabels()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"recipe":{"title":"Makaron","sourceSystem":"Metric","ingredients":["cebula"],"steps":["Smaż cebulę."],"timerLabels":[]},"remainingCredits":5}""");
        var sut = new AiProxyRecipeTranslator(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") }, Identity());

        // Act
        var translation = await sut.TranslateAsync(Recipe(), "pl");

        // Assert
        translation.Steps.Single().Timers.Single().Label.Should().Be("Fry onion");
    }

    [Fact]
    public async Task TranslateAsync_WhenStepCountDiffers_ShouldThrowServiceErrorAfterOneCall()
    {
        // Arrange — the server already retries a step-count mismatch within its own charge, so the
        // client must fail closed on a single response rather than issuing a second (and paying twice).
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"recipe":{"title":"Makaron","sourceSystem":"Metric","ingredients":["cebula"],"steps":["A.","B."]},"remainingCredits":5}""");
        var sut = new AiProxyRecipeTranslator(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") }, Identity());

        // Act
        Func<Task> act = async () => await sut.TranslateAsync(Recipe(), "pl");

        // Assert
        (await act.Should().ThrowAsync<RecipeImportException>()).Which.Code.Should().Be(ImportErrorCode.ServiceError);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task TranslateAsync_WhenIngredientCountDiffers_ShouldThrowServiceError()
    {
        // Arrange — step refs point at ingredients by index, so a merged/dropped line would mislabel them.
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"recipe":{"title":"Makaron","sourceSystem":"Metric","ingredients":["cebula","sól"],"steps":["Smaż cebulę."]},"remainingCredits":5}""");
        var sut = new AiProxyRecipeTranslator(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") }, Identity());

        // Act
        Func<Task> act = async () => await sut.TranslateAsync(Recipe(), "pl");

        // Assert
        (await act.Should().ThrowAsync<RecipeImportException>()).Which.Code.Should().Be(ImportErrorCode.ServiceError);
        handler.CallCount.Should().Be(1);
    }
}
