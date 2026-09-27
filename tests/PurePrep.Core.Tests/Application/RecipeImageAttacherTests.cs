using FluentAssertions;
using NSubstitute;
using PurePrep.Application;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Application;

public sealed class RecipeImageAttacherTests
{
    private readonly IRecipeImageStore _store = Substitute.For<IRecipeImageStore>();
    private readonly IRecipeImageGenerator _generator = Substitute.For<IRecipeImageGenerator>();
    private readonly IRecipeRepository _repository = Substitute.For<IRecipeRepository>();
    private static readonly ParsedRecipe Saved = new() { Title = "Pasta", Ingredients = ["pasta"] };

    [Fact]
    public async Task AttachAsync_WhenImageUrlPresent_ShouldDownloadAndPersistPath()
    {
        // Arrange
        _store.SaveFromUrlAsync(Saved.Id, Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns("images/a.jpg");
        var sut = new RecipeImageAttacher(_store, _generator, _repository);

        // Act
        var result = await sut.AttachAsync(new ParsedImport(Saved, new Uri("https://a.b/p.jpg"), null), Saved, CancellationToken.None);

        // Assert
        result.ImagePath.Should().Be("images/a.jpg");
        await _repository.Received(1).UpdateImagePathAsync(Saved.Id, "images/a.jpg", Arg.Any<CancellationToken>());
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await _generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task AttachAsync_WhenDownloadFailsAndTicketPresent_ShouldGenerate()
    {
        // Arrange
        _store.SaveFromUrlAsync(default, default!, default).ReturnsForAnyArgs((string?)null);
        _generator.GenerateAsync("t1", "Pasta", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(new byte[] { 1 });
        _store.SaveBytesAsync(Saved.Id, Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns("images/g.jpg");
        var sut = new RecipeImageAttacher(_store, _generator, _repository);

        // Act
        var result = await sut.AttachAsync(new ParsedImport(Saved, new Uri("https://a.b/p.jpg"), "t1"), Saved, CancellationToken.None);

        // Assert
        result.ImagePath.Should().Be("images/g.jpg");
    }

    [Fact]
    public async Task AttachAsync_WhenEverythingFails_ShouldReturnRecipeUnchangedWithoutSaving()
    {
        // Arrange
        _generator.GenerateAsync(default!, default!, default!, default).ReturnsForAnyArgs((byte[]?)null);
        var sut = new RecipeImageAttacher(_store, _generator, _repository);

        // Act
        var result = await sut.AttachAsync(new ParsedImport(Saved, null, "t1"), Saved, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(Saved);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await _repository.DidNotReceiveWithAnyArgs().UpdateImagePathAsync(default, default, default);
    }

    [Fact]
    public async Task AttachAsync_WhenGeneratedImage_ShouldShrinkBeforeSaving()
    {
        // Arrange
        var shrinker = Substitute.For<IRecipePhotoShrinker>();
        shrinker.ShrinkAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(new byte[] { 7 });
        _generator.GenerateAsync("t1", "Pasta", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(new byte[] { 1, 2, 3 });
        _store.SaveBytesAsync(Saved.Id, Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns("images/g.jpg");
        var sut = new RecipeImageAttacher(_store, _generator, _repository, shrinker);

        // Act
        await sut.AttachAsync(new ParsedImport(Saved, null, "t1"), Saved, CancellationToken.None);

        // Assert
        await _store.Received(1).SaveBytesAsync(Saved.Id, Arg.Is<byte[]>(b => b.SequenceEqual(new byte[] { 7 })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AttachAsync_WhenGeneratedImageCannotBeDecoded_ShouldKeepPlaceholder()
    {
        // Arrange
        var shrinker = Substitute.For<IRecipePhotoShrinker>();
        shrinker.ShrinkAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _generator.GenerateAsync("t1", "Pasta", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(new byte[] { 1 });
        var sut = new RecipeImageAttacher(_store, _generator, _repository, shrinker);

        // Act
        var result = await sut.AttachAsync(new ParsedImport(Saved, null, "t1"), Saved, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(Saved);
        await _store.DidNotReceiveWithAnyArgs().SaveBytesAsync(default, default!, default);
    }

    [Fact]
    public async Task AttachAsync_WhenDownloadThrows_ShouldStillGenerateFromTicket()
    {
        // Arrange
        _store.SaveFromUrlAsync(default, default!, default).ReturnsForAnyArgs<string?>(_ => throw new InvalidOperationException("boom"));
        _generator.GenerateAsync("t1", "Pasta", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(new byte[] { 1 });
        _store.SaveBytesAsync(Saved.Id, Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns("images/g.jpg");
        var sut = new RecipeImageAttacher(_store, _generator, _repository);

        // Act
        var result = await sut.AttachAsync(new ParsedImport(Saved, new Uri("https://a.b/p.jpg"), "t1"), Saved, CancellationToken.None);

        // Assert
        result.ImagePath.Should().Be("images/g.jpg");
    }

    [Fact]
    public async Task AttachAsync_WhenGeneratorThrows_ShouldKeepPlaceholder()
    {
        // Arrange
        _generator.GenerateAsync(default!, default!, default!, default).ReturnsForAnyArgs<byte[]?>(_ => throw new InvalidOperationException("boom"));
        var sut = new RecipeImageAttacher(_store, _generator, _repository);

        // Act
        var result = await sut.AttachAsync(new ParsedImport(Saved, null, "t1"), Saved, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(Saved);
        await _repository.DidNotReceiveWithAnyArgs().UpdateImagePathAsync(default, default, default);
    }
}
