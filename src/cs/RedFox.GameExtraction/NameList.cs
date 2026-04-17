using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace RedFox.GameExtraction;

/// <summary>
/// Maps numeric asset hashes or identifiers to human-readable names.
/// </summary>
public sealed class NameList
{
    private readonly Dictionary<ulong, string> _names = [];

    /// <summary>
    /// Gets the number of names stored in the lookup.
    /// </summary>
    public int Count => _names.Count;

    /// <summary>
    /// Adds or replaces a name for the supplied hash.
    /// </summary>
    /// <param name="hash">The hash or identifier to map.</param>
    /// <param name="name">The human-readable name to associate with the hash.</param>
    public void Add(ulong hash, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _names[hash] = name;
    }

    /// <summary>
    /// Removes all mappings from the list.
    /// </summary>
    public void Clear() => _names.Clear();

    /// <summary>
    /// Attempts to resolve a name for the supplied hash.
    /// </summary>
    /// <param name="hash">The hash or identifier to resolve.</param>
    /// <param name="name">The resolved name when one is found.</param>
    /// <returns><see langword="true"/> when a name was resolved; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetName(ulong hash, [NotNullWhen(true)] out string? name) => _names.TryGetValue(hash, out name);

    /// <summary>
    /// Loads hash-to-name mappings from a text file.
    /// </summary>
    /// <param name="path">The path of the file to load.</param>
    public void LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        foreach (string line in File.ReadLines(path))
        {
            ReadOnlySpan<char> trimmed = line.AsSpan().Trim();
            if (trimmed.IsEmpty || trimmed.StartsWith("//") || trimmed[0] == '#')
            {
                continue;
            }

            int commaIndex = trimmed.IndexOf(',');
            if (commaIndex > 0)
            {
                ReadOnlySpan<char> hashSpan = trimmed[..commaIndex].Trim();
                ReadOnlySpan<char> nameSpan = trimmed[(commaIndex + 1)..].Trim();

                if (TryParseHash(hashSpan, out ulong hash))
                {
                    _names[hash] = nameSpan.ToString();
                }

                continue;
            }

            string name = trimmed.ToString();
            _names[(ulong)(uint)string.GetHashCode(trimmed, StringComparison.Ordinal)] = name;
        }
    }

    private static bool TryParseHash(ReadOnlySpan<char> span, out ulong hash)
    {
        if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(span[2..], NumberStyles.HexNumber, null, out hash);
        }

        if (ulong.TryParse(span, NumberStyles.HexNumber, null, out hash))
        {
            return true;
        }

        return ulong.TryParse(span, out hash);
    }
}
