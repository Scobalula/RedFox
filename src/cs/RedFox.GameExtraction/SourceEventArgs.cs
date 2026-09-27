namespace RedFox.GameExtraction;

/// <summary>
/// Provides event data for source lifecycle notifications.
/// </summary>
/// <param name="source">The source associated with the event.</param>
public class SourceEventArgs(IAssetSource source) : EventArgs
{
    /// <summary>
    /// Gets the source associated with the event.
    /// </summary>
    public IAssetSource Source { get; } = source ?? throw new ArgumentNullException(nameof(source));
}
