using System.Diagnostics.CodeAnalysis;

namespace RedFox.GameExtraction.Tests;

internal sealed class ReadPurposeTestSource : IAssetSource
{
    public string Name => "Read purpose tests";
    public IReadOnlyList<Asset> Assets { get; }

    public ReadPurposeTestSource(Asset[] assets)
    {
        Assets = assets;
    }

    public bool TryGetAsset(string path, [NotNullWhen(true)] out Asset? asset)
    {
        asset = Assets.FirstOrDefault(candidate => candidate.Name == path);
        return asset is not null;
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
