using System.Diagnostics.CodeAnalysis;
using RedFox.Graphics3D;

namespace RedFox.Graphics3D.IO;

/// <summary>
/// Provides configuration settings for scene translation, including built-in properties
/// and an extensible dictionary for format-specific options.
/// </summary>
public class SceneTranslatorOptions
{
    private readonly Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets whether raw vertex data (positions, normals, UVs) should be written
    /// without taking bind matrices into account.
    /// </summary>
    public bool WriteRawVertices { get; set; }

    /// <summary>
    /// Gets or sets a node flag filter that translators may use to restrict which scene nodes are exported.
    /// Translators that do not support filtered export may ignore this option.
    /// </summary>
    public SceneNodeFlags Filter { get; set; }

    /// <summary>
    /// Gets the full source file path for the current read operation.
    /// Set internally by the translation manager.
    /// </summary>
    public string? SourceFilePath { get; internal set; }

    /// <summary>
    /// Gets the source directory for the current read operation.
    /// Set internally by the translation manager.
    /// </summary>
    public string? SourceDirectoryPath { get; internal set; }

    /// <summary>
    /// Gets a format-specific option by key.
    /// </summary>
    /// <typeparam name="T">The expected type of the option value.</typeparam>
    /// <param name="key">The case-insensitive key of the option.</param>
    /// <returns>The option value, or <see langword="default"/> if the key was not found or the value is not of type <typeparamref name="T"/>.</returns>
    public T? Get<T>(string key)
    {
        return _properties.TryGetValue(key, out var value) && value is T typed ? typed : default;
    }

    /// <summary>
    /// Sets a format-specific option.
    /// </summary>
    /// <typeparam name="T">The type of the option value.</typeparam>
    /// <param name="key">The case-insensitive key of the option.</param>
    /// <param name="value">The value to store.</param>
    public void Set<T>(string key, T? value)
    {
        _properties[key] = value;
    }

    /// <summary>
    /// Attempts to retrieve a format-specific option by key.
    /// </summary>
    /// <typeparam name="T">The expected type of the option value.</typeparam>
    /// <param name="key">The case-insensitive key of the option.</param>
    /// <param name="value">When this method returns, contains the option value if found and of the correct type; otherwise, <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if the key exists and the value is of type <typeparamref name="T"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGet<T>(string key, [MaybeNullWhen(false)] out T value)
    {
        if (_properties.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Determines whether an option with the specified key exists.
    /// </summary>
    /// <param name="key">The case-insensitive key to look up.</param>
    /// <returns><see langword="true"/> if the key exists; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string key) => _properties.ContainsKey(key);

    /// <summary>
    /// Removes the option with the specified key.
    /// </summary>
    /// <param name="key">The case-insensitive key to remove.</param>
    /// <returns><see langword="true"/> if the key was found and removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(string key) => _properties.Remove(key);
}
