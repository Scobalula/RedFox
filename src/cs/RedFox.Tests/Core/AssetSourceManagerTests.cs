using RedFox.GameExtraction;

namespace RedFox.Tests.Core;

public sealed class AssetSourceManagerTests
{
    [Fact]
    public void RegisterFileReader_RejectsDuplicateExtensionsAcrossReaders()
    {
        using var manager = new AssetSourceManager<string>();

        manager.RegisterFileReader(new TestFileReader([".zip", ".pak"]));

        var exception = Assert.Throws<InvalidOperationException>(() => manager.RegisterFileReader(new TestFileReader([".pak"])));
        Assert.Contains(".pak", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadFromFile_MatchesAnyRegisteredExtension()
    {
        using var manager = new AssetSourceManager<string>();
        manager.RegisterFileReader(new TestFileReader([".foo", ".bar"]));

        var result = manager.LoadFromFile("example.bar");

        Assert.Equal("example.bar", result.Source.Location);
        Assert.Single(result.Assets);
    }

    [Fact]
    public void Dispose_DisposesTrackedSources()
    {
        var source = new TestSource();
        var manager = new AssetSourceManager<string>();
        manager.RegisterFileReader(new TestFileReader([".foo"], source));

        manager.LoadFromFile("example.foo");
        manager.Dispose();

        Assert.True(source.IsDisposed);
    }

    private sealed class TestFileReader(IReadOnlyList<string> extensions, TestSource? sharedSource = null) : IFileAssetSourceReader<string>
    {
        public IReadOnlyList<string> Extensions { get; } = extensions;

        public string FileFilter => "Test Files|*.*";

        public AssetSourceLoadResult<string> LoadFromFile(string filePath, IProgress<(int Current, int Total, string Status)>? progress = null, CancellationToken cancellationToken = default)
        {
            return new AssetSourceLoadResult<string>
            {
                Source = sharedSource ?? new TestSource { Location = filePath },
                Assets = [filePath],
            };
        }
    }

    private sealed class TestSource : IAssetSource
    {
        public string DisplayName { get; init; } = "test";

        public string? Location { get; init; }

        public bool IsDisposed { get; private set; }

        public Guid Id => throw new NotImplementedException();

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}