using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// Presents a mounted asset source to Avalonia views.
/// </summary>
public sealed class AssetSourceViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetSourceViewModel"/> class.
    /// </summary>
    /// <param name="source">The mounted source.</param>
    /// <param name="request">The request that produced the source, when available.</param>
    public AssetSourceViewModel(IAssetSource source, AssetSourceRequest? request)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Request = request;
    }

    /// <summary>
    /// Gets the mounted source.
    /// </summary>
    public IAssetSource Source { get; }

    /// <summary>
    /// Gets the request that produced the source, when available.
    /// </summary>
    public AssetSourceRequest? Request { get; }

    /// <summary>
    /// Gets the display name for the source.
    /// </summary>
    public string DisplayName => Source.Name;

    /// <summary>
    /// Gets the display location for the source.
    /// </summary>
    public string Location => Request switch
    {
        { Location: { Length: > 0 } location } => location,
        { ProcessId: int processId } => $"PID {processId}",
        { ProcessName: { Length: > 0 } processName } => processName,
        _ => string.Empty,
    };

    /// <summary>
    /// Gets the source kind text.
    /// </summary>
    public string Kind => Request?.Kind.ToString() ?? "Mounted";

    /// <summary>
    /// Gets the number of assets in the source.
    /// </summary>
    public int AssetCount => Source.Assets.Count;
}
