namespace RedFox.GameExtraction;

/// <summary>
/// Represents a loaded asset source owned by a manager.
/// </summary>
/// <remarks>
/// A source can represent a file archive, an in-memory asset pool, a container,
/// or any other backing store that contributes assets.
/// </remarks>
public interface IAssetSource : IDisposable
{
    /// <summary>
    /// Gets the stable identifier for this source.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the display name for this source.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the backing location for this source, if any.
    /// </summary>
    string? Location { get; }
}