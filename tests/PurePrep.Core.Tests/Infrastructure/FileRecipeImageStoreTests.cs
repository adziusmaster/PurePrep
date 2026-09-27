using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using NSubstitute;
using PurePrep.Application;
using PurePrep.Core.Tests.TestSupport;
using PurePrep.Infrastructure;

namespace PurePrep.Core.Tests.Infrastructure;

public sealed class FileRecipeImageStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pp-img-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SaveFromUrlAsync_WhenImageResponse_ShouldWriteFileAndReturnRelativePath()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Bytes(HttpStatusCode.OK, [1, 2, 3], "image/jpeg");
        var store = new FileRecipeImageStore(new HttpClient(handler), _root);
        var id = Guid.NewGuid();

        // Act
        var path = await store.SaveFromUrlAsync(id, new Uri("https://a.b/p.jpg"), CancellationToken.None);

        // Assert
        path.Should().Be($"images/{id}.jpg");
        (await store.ReadAsync(path!, CancellationToken.None)).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task SaveFromUrlAsync_WhenDownloading_ShouldSendAUserAgent()
    {
        // Arrange — RecipeTin Eats' CDN (CloudFront) answers 403 to a request without a User-Agent,
        // and .NET's HttpClient sends none by default.
        var handler = StubHttpMessageHandler.Bytes(HttpStatusCode.OK, [1, 2, 3], "image/jpeg");
        var store = new FileRecipeImageStore(new HttpClient(handler), _root);

        // Act
        await store.SaveFromUrlAsync(Guid.NewGuid(), new Uri("https://a.b/p.jpg"), CancellationToken.None);

        // Assert
        handler.SentRequests.Should().ContainSingle().Which.UserAgent.Should().StartWith("PurePrep");
    }

    [Fact]
    public async Task SaveFromUrlAsync_WhenHandlerThrowsWebException_ShouldReturnNull()
    {
        // Arrange — Android's AndroidMessageHandler reports timeouts, TLS and I/O failures as
        // WebException, which is neither an HttpRequestException nor a System.IO.IOException.
        var store = new FileRecipeImageStore(new HttpClient(new ThrowingHandler(new WebException("Read error"))), _root);

        // Act
        var path = await store.SaveFromUrlAsync(Guid.NewGuid(), new Uri("https://a.b/p.jpg"), CancellationToken.None);

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public async Task SaveFromUrlAsync_WhenCallerCancels_ShouldThrowOperationCanceled()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Bytes(HttpStatusCode.OK, [1, 2, 3], "image/jpeg");
        var store = new FileRecipeImageStore(new HttpClient(handler), _root);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        Func<Task> act = async () => await store.SaveFromUrlAsync(Guid.NewGuid(), new Uri("https://a.b/p.jpg"), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SaveFromUrlAsync_WhenSiteForbidsTheRequest_ShouldReturnNull()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Bytes(HttpStatusCode.Forbidden, "<html>"u8.ToArray(), "text/html");
        var store = new FileRecipeImageStore(new HttpClient(handler), _root);

        // Act
        var path = await store.SaveFromUrlAsync(Guid.NewGuid(), new Uri("https://a.b/p.jpg"), CancellationToken.None);

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public async Task SaveFromUrlAsync_WhenShrinkerPresent_ShouldStoreShrunkBytes()
    {
        // Arrange
        var shrinker = Substitute.For<IRecipePhotoShrinker>();
        shrinker.ShrinkAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(new byte[] { 9 });
        var handler = StubHttpMessageHandler.Bytes(HttpStatusCode.OK, [1, 2, 3], "image/jpeg");
        var store = new FileRecipeImageStore(new HttpClient(handler), _root, shrinker);

        // Act
        var path = await store.SaveFromUrlAsync(Guid.NewGuid(), new Uri("https://a.b/p.jpg"), CancellationToken.None);

        // Assert
        (await store.ReadAsync(path!, CancellationToken.None)).Should().Equal(9);
    }

    [Fact]
    public async Task SaveFromUrlAsync_WhenImageCannotBeDecoded_ShouldReturnNull()
    {
        // Arrange
        var shrinker = Substitute.For<IRecipePhotoShrinker>();
        shrinker.ShrinkAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        var handler = StubHttpMessageHandler.Bytes(HttpStatusCode.OK, [1, 2, 3], "image/jpeg");
        var store = new FileRecipeImageStore(new HttpClient(handler), _root, shrinker);

        // Act
        var path = await store.SaveFromUrlAsync(Guid.NewGuid(), new Uri("https://a.b/p.jpg"), CancellationToken.None);

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public async Task SaveFromUrlAsync_WhenResponseIsNotAnImage_ShouldReturnNull()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Bytes(HttpStatusCode.OK, "<html>"u8.ToArray(), "text/html");
        var store = new FileRecipeImageStore(new HttpClient(handler), _root);

        // Act
        var path = await store.SaveFromUrlAsync(Guid.NewGuid(), new Uri("https://a.b/p.jpg"), CancellationToken.None);

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public async Task SaveFromUrlAsync_WhenImageLargerThanFiveMegabytes_ShouldReturnNull()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Bytes(HttpStatusCode.OK, new byte[5 * 1024 * 1024 + 1], "image/jpeg");
        var store = new FileRecipeImageStore(new HttpClient(handler), _root);

        // Act
        var path = await store.SaveFromUrlAsync(Guid.NewGuid(), new Uri("https://a.b/p.jpg"), CancellationToken.None);

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public async Task SaveFromUrlAsync_WhenContentLengthMissingAndBodyExceedsFiveMegabytes_ShouldReturnNull()
    {
        // Arrange: no Content-Length header (a non-seekable stream, like chunked transfer), so the only
        // way to catch an oversized body is by counting bytes as they stream in.
        var handler = new NoContentLengthHandler(new byte[5 * 1024 * 1024 + 1]);
        var store = new FileRecipeImageStore(new HttpClient(handler), _root);

        // Act
        var path = await store.SaveFromUrlAsync(Guid.NewGuid(), new Uri("https://a.b/p.jpg"), CancellationToken.None);

        // Assert
        path.Should().BeNull();
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("images/../../etc/passwd")]
    public void FullPath_WhenPathEscapesRoot_ShouldReturnNull(string path)
    {
        // Arrange
        var store = new FileRecipeImageStore(new HttpClient(), _root);

        // Act
        var result = store.FullPath(path);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FullPath_WhenPathIsUnderRoot_ShouldReturnPathUnderRoot()
    {
        // Arrange
        var store = new FileRecipeImageStore(new HttpClient(), _root);

        // Act
        var result = store.FullPath("images/a.jpg");

        // Assert
        result.Should().Be(Path.GetFullPath(Path.Combine(_root, "images/a.jpg")));
    }

    /// <summary>Answers with a body that has no Content-Length header, over a non-seekable stream —
    /// the shape a chunked-transfer response takes.</summary>
    private sealed class NoContentLengthHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new StreamContent(new NonSeekableStream(body));
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    /// <summary>A read-only stream that reports <see cref="CanSeek"/> false, so <see cref="StreamContent"/>
    /// can't precompute a Content-Length from it.</summary>
    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = data.Length - _position;
            if (remaining <= 0)
                return 0;
            var toCopy = Math.Min(count, remaining);
            Array.Copy(data, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
