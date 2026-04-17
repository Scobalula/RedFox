namespace RedFox.GameExtraction;

/// <summary>
/// Provides event data for source lifecycle notifications.
/// </summary>
public class SourceEventArgs : EventArgs
{
    /// <summary>
    /// Gets the source associated with the event.
    /// </summary>
    public IAssetSource Source { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceEventArgs"/> class.
    /// </summary>
    /// <param name="source">The source associated with the event.</param>
    public SourceEventArgs(IAssetSource source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }
}
