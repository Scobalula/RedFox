namespace RedFox.GameExtraction;

/// <summary>
/// Represents the result of reading an asset.
/// </summary>
public sealed class AssetReadResult
{
    private readonly List<AssetReadResult> _references = [];

    /// <summary>
    /// Gets the asset associated with the read.
    /// </summary>
    public required Asset Asset { get; init; }

    /// <summary>
    /// Gets the asset handler that provided this result.
    /// </summary>
    public required IAssetHandler Handler { get; set; }

    /// <summary>
    /// Gets the decoded payload returned by the handler, or <see langword="null"/> when no payload is produced.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Gets the read results of any assets that were read as references while this result was being produced.
    /// </summary>
    /// <remarks>
    /// Populated by <see cref="AssetManager"/> when a handler resolves a child read through the read context
    /// that produced this result. Exporters and other downstream consumers can iterate the list to discover
    /// every asset pulled in as a dependency for the asset associated with this result.
    /// </remarks>
    public IReadOnlyList<AssetReadResult> References => _references;

    /// <summary>
    /// Returns <see cref="Data"/> cast to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The expected payload type.</typeparam>
    /// <returns>The payload cast to <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the payload cannot be cast to <typeparamref name="T"/>.</exception>
    public T GetData<T>()
    {
        if (Data is T value)
        {
            return value;
        }

        string actual = Data is null ? "null" : Data.GetType().FullName ?? Data.GetType().Name;
        throw new InvalidOperationException($"Expected payload of type '{typeof(T).FullName}' but the result carries '{actual}'.");
    }

    internal void AddReference(AssetReadResult reference) => _references.Add(reference);
}
