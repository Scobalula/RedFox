// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Represents a name table that maps <see cref="NameKey"/> values to their corresponding string names.
/// </summary>
/// <param name="hashAlgorithm">The name of the hash algorithm used to generate the keys in this table.</param>
public sealed class NameTable(string hashAlgorithm) : IEnumerable<KeyValuePair<NameKey, string>>
{
    private readonly Dictionary<NameKey, string> _entries = [];

    /// <summary>
    /// Gets or sets the name of the hash algorithm used to generate the keys in this table.
    /// </summary>
    public string HashAlgorithm { get; set; } = hashAlgorithm;

    /// <summary>
    /// Gets the number of entries in the name table.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Gets or sets optional metadata associated with the name table, such as source information or comments.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>
    /// Adds an entry to the name table, overwriting any existing entry with the same key.
    /// </summary>
    /// <param name="key">The hash key of the name.</param>
    /// <param name="value">The string name associated with the key.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is <see langword="null"/> or whitespace.</exception>
    public void Add(NameKey key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _entries[key] = value;
    }

    /// <summary>
    /// Adds an entry to the name table using a byte array key, overwriting any existing entry with the same key.
    /// </summary>
    /// <param name="key">The byte array representing the hash key.</param>
    /// <param name="value">The string name associated with the key.</param>
    public void Add(byte[] key, string value) => Add(new NameKey(key), value);

    /// <summary>
    /// Attempts to retrieve the string name associated with the specified key.
    /// </summary>
    /// <param name="key">The hash key to look up.</param>
    /// <param name="value">When this method returns, contains the string name if the key was found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the key was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(NameKey key, [NotNullWhen(true)] out string? value) => _entries.TryGetValue(key, out value);

    /// <summary>
    /// Attempts to retrieve the string name associated with the specified byte array key.
    /// </summary>
    /// <param name="key">The byte array representing the hash key to look up.</param>
    /// <param name="value">When this method returns, contains the string name if the key was found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the key was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(byte[] key, [NotNullWhen(true)] out string? value) => TryGetValue(new NameKey(key), out value);

    /// <summary>
    /// Removes all entries from the name table.
    /// </summary>
    public void Clear() => _entries.Clear();

    /// <summary>
    /// Returns an enumerator that iterates through the entries in the name table.
    /// </summary>
    /// <returns>A <see cref="Dictionary{TKey,TValue}.Enumerator"/> for the name table.</returns>
    public Dictionary<NameKey, string>.Enumerator GetEnumerator() => _entries.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator<KeyValuePair<NameKey, string>> IEnumerable<KeyValuePair<NameKey, string>>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
