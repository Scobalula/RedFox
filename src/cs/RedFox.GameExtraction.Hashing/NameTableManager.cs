// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Manages multiple name tables and hash algorithm registrations, providing centralized lookup and persistence.
/// </summary>
public sealed class NameTableManager
{
    private readonly List<NameTable> _tables = [];
    private readonly Dictionary<string, Func<ReadOnlySpan<char>, NameKey>> _hashers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the list of all loaded name tables.
    /// </summary>
    public IReadOnlyList<NameTable> Tables => _tables;

    /// <summary>
    /// Gets the total number of entries across all loaded name tables.
    /// </summary>
    public int TotalEntries => _tables.Sum(static t => t.Count);

    /// <summary>
    /// Registers a hash algorithm and its hashing function for use when loading .txt name files.
    /// </summary>
    /// <param name="algorithm">The name of the hash algorithm.</param>
    /// <param name="hasher">The function that hashes a string name into a <see cref="NameKey"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="algorithm"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hasher"/> is <see langword="null"/>.</exception>
    public void RegisterHasher(string algorithm, Func<ReadOnlySpan<char>, NameKey> hasher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentNullException.ThrowIfNull(hasher);

        _hashers[algorithm] = hasher;
    }

    /// <summary>
    /// Determines whether a hash algorithm with the specified name has been registered.
    /// </summary>
    /// <param name="algorithm">The name of the hash algorithm.</param>
    /// <returns><see langword="true"/> if the algorithm is registered; otherwise, <see langword="false"/>.</returns>
    public bool HasAlgorithm(string algorithm)
    {
        return _hashers.ContainsKey(algorithm);
    }

    /// <summary>
    /// Loads a name table from a file, automatically detecting the format and adding it to the managed tables.
    /// </summary>
    /// <param name="filePath">The path to the name file.</param>
    /// <returns>The loaded name table.</returns>
    /// <exception cref="InvalidOperationException">Thrown when loading a .txt file and no matching hash algorithm is registered.</exception>
    public NameTable Load(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext == ".txt")
        {
            var algorithm = NameFile.DeriveAlgorithm(filePath);

            if (!_hashers.TryGetValue(algorithm, out var hasher))
                throw new InvalidOperationException($"No hash algorithm registered with the name '{algorithm}' (derived from '{filePath}'). Register it first or use Load(filePath, algorithm).");

            var table = NameFile.Load(filePath, algorithm, hasher);
            _tables.Add(table);
            return table;
        }

        var loaded = NameFile.Load(filePath);
        _tables.Add(loaded);
        return loaded;
    }

    /// <summary>
    /// Loads a name table from a file with an explicit hash algorithm and adds it to the managed tables.
    /// </summary>
    /// <param name="filePath">The path to the name file.</param>
    /// <param name="hashAlgorithm">The name of the hash algorithm used to generate the file.</param>
    /// <returns>The loaded name table.</returns>
    /// <exception cref="InvalidOperationException">Thrown when loading a .txt file and no matching hash algorithm is registered.</exception>
    public NameTable Load(string filePath, string hashAlgorithm)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        NameTable table;

        if (ext == ".txt")
        {
            if (!_hashers.TryGetValue(hashAlgorithm, out var hasher))
                throw new InvalidOperationException($"No hash algorithm registered with the name '{hashAlgorithm}'. Call Register() first.");

            table = NameFile.Load(filePath, hashAlgorithm, hasher);
        }
        else
        {
            table = NameFile.Load(filePath, hashAlgorithm);
        }

        _tables.Add(table);
        return table;
    }

    /// <summary>
    /// Adds an existing name table to the manager.
    /// </summary>
    /// <param name="table">The name table to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="table"/> is <see langword="null"/>.</exception>
    public void Add(NameTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _tables.Add(table);
    }

    /// <summary>
    /// Removes a name table from the manager.
    /// </summary>
    /// <param name="table">The name table to remove.</param>
    /// <returns><see langword="true"/> if the table was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(NameTable table)
    {
        return _tables.Remove(table);
    }

    /// <summary>
    /// Removes all managed name tables.
    /// </summary>
    public void Clear()
    {
        _tables.Clear();
    }

    /// <summary>
    /// Attempts to retrieve the string name associated with the specified key, searching only tables matching the given algorithm.
    /// </summary>
    /// <param name="algorithm">The name of the hash algorithm to search within.</param>
    /// <param name="key">The hash key to look up.</param>
    /// <param name="value">When this method returns, contains the string name if the key was found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the key was found in a matching table; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(string algorithm, NameKey key, [NotNullWhen(true)] out string? value)
    {
        foreach (var table in _tables)
        {
            if (!string.Equals(table.Name, algorithm, StringComparison.OrdinalIgnoreCase))
                continue;

            if (table.TryGetValue(key, out value))
                return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve the string name associated with the specified key across all managed name tables.
    /// </summary>
    /// <param name="key">The hash key to look up.</param>
    /// <param name="value">When this method returns, contains the string name if the key was found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the key was found in any table; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(NameKey key, [NotNullWhen(true)] out string? value)
    {
        foreach (var table in _tables)
        {
            if (table.TryGetValue(key, out value))
                return true;
        }

        value = null;
        return false;
    }

    public NameTable CreateNameTable(string name)
    {
        var table = new NameTable(name);
        Add(table);
        return table;
    }

    public bool TryGetTable(string name, [NotNullWhen(true)] out NameTable? nameTable)
    {
        foreach (var table in _tables)
        {
            if (!string.Equals(table.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                nameTable = table;
                return true;
            }
        }

        nameTable = null;
        return false;
    }
}
