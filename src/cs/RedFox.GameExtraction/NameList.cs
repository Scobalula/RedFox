using System.Globalization;
using System.Runtime.CompilerServices;

namespace RedFox.GameExtraction;

/// <summary>
/// Maps numeric asset hashes or identifiers to human-readable name strings.
/// Used when an archive stores assets by hash rather than a full path.
/// </summary>
public sealed class NameList
{
    private readonly Dictionary<ulong, string> _names = [];

    /// <summary>Number of entries currently held.</summary>
    public int Count => _names.Count;

    /// <summary>Adds or updates a single hash-to-name mapping.</summary>
    /// <param name="hash">The numeric hash or identifier.</param>
    /// <param name="name">The human-readable name to associate with <paramref name="hash"/>.</param>
    public void Add(ulong hash, string name) => _names[hash] = name;

    /// <summary>Removes all entries.</summary>
    public void Clear() => _names.Clear();

    /// <summary>Tries to resolve a hash to its registered name.</summary>
    /// <param name="hash">The numeric hash or identifier to look up.</param>
    /// <param name="name">The resolved name, or <see langword="null"/> if not found.</param>
    /// <returns><see langword="true"/> if the hash was resolved; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetName(ulong hash, out string? name) => _names.TryGetValue(hash, out name);

    /// <summary>
    /// Loads entries from a plain-text file, merging them into the current list.
    /// </summary>
    /// <remarks>
    /// Each non-empty, non-comment line is parsed in one of two forms:
    /// <list type="bullet">
    ///   <item><description><c>hash,name</c> — maps a numeric hash (decimal or hex with/without <c>0x</c> prefix) to a name.</description></item>
    ///   <item><description><c>name</c> — bare name; stored with its own <see cref="string.GetHashCode()"/> as placeholder key.</description></item>
    /// </list>
    /// Lines beginning with <c>//</c> or <c>#</c> are treated as comments and skipped.
    /// </remarks>
    /// <param name="path">Absolute path to the name list file.</param>
    public void LoadFromFile(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.AsSpan().Trim();
            if (trimmed.IsEmpty || trimmed.StartsWith("//") || trimmed[0] == '#')
                continue;

            var commaIndex = trimmed.IndexOf(',');
            if (commaIndex > 0)
            {
                var hashSpan = trimmed[..commaIndex].Trim();
                var nameSpan = trimmed[(commaIndex + 1)..].Trim();

                if (TryParseHash(hashSpan, out var hash))
                    _names[hash] = nameSpan.ToString();
            }
            else
            {
                _names[(ulong)(uint)string.GetHashCode(trimmed, StringComparison.Ordinal)] = trimmed.ToString();
            }
        }
    }

    private static bool TryParseHash(ReadOnlySpan<char> span, out ulong hash)
    {
        if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(span[2..], NumberStyles.HexNumber, null, out hash);

        if (ulong.TryParse(span, NumberStyles.HexNumber, null, out hash))
            return true;

        return ulong.TryParse(span, out hash);
    }
}
