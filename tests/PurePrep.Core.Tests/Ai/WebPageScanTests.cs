using FluentAssertions;
using PurePrep.Ai;

namespace PurePrep.Core.Tests.Ai;

public sealed class WebPageScanTests
{
    private const string Payload =
        """{"ld":["{\"@type\":\"Recipe\"}"],"mdName":"Pierogi","mdImage":"/p.jpg","title":"Pierogi | Site"}""";

    [Fact]
    public void Decode_WhenPlainJsonObject_ShouldReadAllFields()
    {
        // Arrange
        var result = Payload;

        // Act
        var scan = WebPageScan.Decode(result);

        // Assert
        scan.Should().NotBeNull();
        scan!.JsonLdBlocks.Should().Equal("""{"@type":"Recipe"}""");
        scan.MicrodataName.Should().Be("Pierogi");
        scan.MicrodataImage.Should().Be("/p.jpg");
        scan.Title.Should().Be("Pierogi | Site");
    }

    [Fact]
    public void Decode_WhenPlainJsonArray_ShouldTreatItAsJsonLdBlocks()
    {
        // Arrange
        var result = """["a","b"]""";

        // Act
        var scan = WebPageScan.Decode(result);

        // Assert
        scan.Should().BeEquivalentTo(new WebPageScan(new[] { "a", "b" }, null, null, null));
    }

    [Fact]
    public void Decode_WhenDoubleEncodedString_ShouldPeelBothLayers()
    {
        // Arrange
        var once = System.Text.Json.JsonSerializer.Serialize(Payload);
        var twice = System.Text.Json.JsonSerializer.Serialize(once);

        // Act
        var scan = WebPageScan.Decode(twice);

        // Assert
        scan!.MicrodataName.Should().Be("Pierogi");
        scan.JsonLdBlocks.Should().ContainSingle();
    }

    [Fact]
    public void Decode_WhenQuoteStrippedEscapedString_ShouldDecode()
    {
        // Arrange: MAUI trims the outer quotes of the JSON string Android returns.
        var encoded = System.Text.Json.JsonSerializer.Serialize(Payload);
        var stripped = encoded.Trim('"');

        // Act
        var scan = WebPageScan.Decode(stripped);

        // Assert
        scan!.Title.Should().Be("Pierogi | Site");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{not json")]
    [InlineData("42")]
    public void Decode_WhenNullOrMalformed_ShouldReturnNull(string? result)
    {
        // Arrange / Act
        var scan = WebPageScan.Decode(result);

        // Assert
        scan.Should().BeNull();
    }

    [Fact]
    public void Decode_WhenFieldsHaveOddShapes_ShouldIgnoreThemWithoutThrowing()
    {
        // Arrange
        var result = """{"ld":[1,"ok",null],"mdName":5,"mdImage":{},"title":null}""";

        // Act
        var scan = WebPageScan.Decode(result);

        // Assert
        scan!.JsonLdBlocks.Should().Equal("ok");
        scan.MicrodataName.Should().BeNull();
        scan.MicrodataImage.Should().BeNull();
        scan.Title.Should().BeNull();
    }

    [Fact]
    public void Decode_WhenFieldsHaveUnpairedSurrogates_ShouldDropThemWithoutThrowing()
    {
        // Arrange
        var result = """{"ld":["\ud800x","ok"],"mdName":"\ud800x","mdImage":"\udc00","title":"T"}""";

        // Act
        var scan = WebPageScan.Decode(result);

        // Assert
        scan!.JsonLdBlocks.Should().Equal("ok");
        scan.MicrodataName.Should().BeNull();
        scan.MicrodataImage.Should().BeNull();
        scan.Title.Should().Be("T");
    }

    [Fact]
    public void Decode_WhenOuterStringHasUnpairedSurrogate_ShouldReturnNull()
    {
        // Arrange
        var result = "\"\\ud800x\"";

        // Act
        var scan = WebPageScan.Decode(result);

        // Assert
        scan.Should().BeNull();
    }

    [Fact]
    public void Decode_WhenResultIsLargerThanCap_ShouldReturnEmptyScan()
    {
        // Arrange
        var result = "[\"" + new string('a', WebPageScan.MaxResultLength) + "\"]";

        // Act
        var scan = WebPageScan.Decode(result);

        // Assert
        scan.Should().BeEquivalentTo(new WebPageScan([], null, null, null));
    }
}
