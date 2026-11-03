namespace RedFox.GameExtraction.UI.ViewModels;

internal sealed class SourceLoadProgress(Action<string> statusCallback, Action<AssetSourceLoadProgress> loadCallback) : IProgress<string>, IProgress<AssetSourceLoadProgress>
{
    public void Report(string value) => statusCallback(value);

    public void Report(AssetSourceLoadProgress value) => loadCallback(value);
}
