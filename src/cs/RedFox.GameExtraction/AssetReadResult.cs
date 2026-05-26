namespace RedFox.GameExtraction;

/// <summary>
/// Represents the result of reading an asset.
/// </summary>
public sealed class AssetReadResult
{
    /// <summary>
    /// Gets the asset associated with the read.
    /// </summary>
    public required Asset Asset { get; init; }

    /// <summary>
    /// Gets the decoded payload returned by the handler, or <see langword="null"/> when no payload is produced.
    /// </summary>
    public object? Data { get; init; }

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
        throw new InvalidOperationException(
            $"Expected payload of type '{typeof(T).FullName}' but the result carries '{actual}'.");
    }
}
