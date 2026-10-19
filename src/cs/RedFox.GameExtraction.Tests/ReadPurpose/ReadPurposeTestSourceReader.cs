namespace RedFox.GameExtraction.Tests;

internal sealed class ReadPurposeTestSourceReader : IAssetSourceReader
{
    private readonly Asset[] _assets;

    public ReadPurposeTestSourceReader(Asset[] assets)
    {
        _assets = assets;
    }

    public bool CanOpen(AssetSourceRequest request) => true;

    public Task<IAssetSource> OpenAsync(AssetSourceRequest request, AssetManager assetManager, IProgress<string>? progress, CancellationToken cancellationToken) => Task.FromResult<IAssetSource>(new ReadPurposeTestSource(_assets));
}
