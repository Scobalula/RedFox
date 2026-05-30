using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace RedFox.GameExtraction;

/// <summary>
/// Controls how assets are exported to disk.
/// </summary>
public sealed class ExportConfiguration
{
    /// <summary>
    /// Gets the root directory used for exported output.
    /// </summary>
    public required string OutputDirectory { get; init; }

    /// <summary>
    /// Gets a value indicating whether existing files may be overwritten.
    /// </summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Gets a value indicating whether referenced assets should be exported recursively.
    /// </summary>
    public bool ExportReferences { get; init; }

    /// <summary>
    /// Gets a value indicating whether asset directory structure should be preserved under the output root.
    /// </summary>
    public bool PreserveDirectoryStructure { get; init; } = true;

    /// <summary>
    /// Gets arbitrary export options that handlers can interpret as needed.
    /// </summary>
    public Dictionary<string, object>? Options { get; set; }

    /// <summary>
    /// Sets a custom Option with the given key and value.
    /// </summary>
    /// <param name="key">The key/name of the Option.</param>
    /// <param name="value">The value of the Option.</param>
    public void SetOption(string key, object value)
    {
        Options ??= [];
        Options[key] = value;
    }

    /// <summary>
    /// Attempts to get a custom Option by key.
    /// </summary>
    /// <param name="key">The key/name of the Option.</param>
    /// <param name="value">The value of the Option.</param>
    /// <returns><see langword="true"/> if the Option was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetOption(string key, [NotNullWhen(true)] out object? value)
    {
        value = null;
        return Options != null && Options.TryGetValue(key, out value);
    }

    /// <summary>
    /// Attempts to get a custom Option by key.
    /// </summary>
    /// <typeparam name="T">The type of the Option.</typeparam>
    /// <param name="key">The key/name of the Option.</param>
    /// <param name="value">The value of the Option.</param>
    /// <returns><see langword="true"/> if the Option was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetOption<T>(string key, [NotNullWhen(true)] out T? value)
    {
        value = default;
        if (Options != null && Options.TryGetValue(key, out var objValue) && objValue is T tValue)
        {
            value = tValue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a custom Option by key.
    /// </summary>
    /// <param name="key">The key/name of the Option.</param>
    /// <returns>The value of the Option.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the Option with the specified key is not found.</exception>
    public object GetOption(string key)
    {
        if (Options != null && Options.TryGetValue(key, out var value))
        {
            return value;
        }
        throw new KeyNotFoundException($"Option with key '{key}' not found.");
    }

    /// <summary>
    /// Gets a custom Option by key.
    /// </summary>
    /// <typeparam name="T">The type of the Option.</typeparam>
    /// <param name="key">The key/name of the Option.</param>
    /// <returns>The value of the Option.</returns>
    /// <exception cref="InvalidCastException">Thrown when the Option with the specified key is not of the expected type.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the Option with the specified key is not found.</exception>
    public T GetOption<T>(string key)
    {
        if (Options != null && Options.TryGetValue(key, out var value))
        {
            if (value is T tValue)
            {
                return tValue;
            }
            throw new InvalidCastException($"Option with key '{key}' is not of type {typeof(T).FullName}.");
        }
        throw new KeyNotFoundException($"Option with key '{key}' not found.");
    }

    /// <summary>
    /// Gets a custom Option by key.
    /// </summary>
    /// <param name="key">The key/name of the Option.</param>
    /// <param name="defaultValue">The default value to return if the Option is not found.</param>
    /// <returns>The value of the Option.</returns>
    public object GetOption(string key, object defaultValue)
    {
        if (Options != null && Options.TryGetValue(key, out var value))
        {
            return value;
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets a custom Option by key.
    /// </summary>
    /// <typeparam name="T">The type of the Option.</typeparam>
    /// <param name="key">The key/name of the Option.</param>
    /// <param name="defaultValue">The default value to return if the Option is not found.</param>
    /// <returns>The value of the Option.</returns>
    /// <exception cref="InvalidCastException">Thrown when the Option with the specified key is not of the expected type.</exception>
    public T GetOption<T>(string key, T defaultValue)
    {
        if (Options != null && Options.TryGetValue(key, out var value))
        {
            if (value is T tValue)
            {
                return tValue;
            }
            throw new InvalidCastException($"Option with key '{key}' is not of type {typeof(T).FullName}.");
        }
        return defaultValue;
    }
}
