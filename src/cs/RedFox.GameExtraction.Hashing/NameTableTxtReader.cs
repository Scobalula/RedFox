// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Provides methods for parsing name tables from a simple text file.
/// </summary>
public static class NameTableTxtReader
{
    /// <summary>
    /// Parses a name table from a text file, where each line contains a name to hash.
    /// </summary>
    /// <param name="filePath">The path to the text file.</param>
    /// <param name="hashAlgorithm">The hash algorithm that was used to generate the file.</param>
    /// <param name="hasher">The function to use for hashing names.</param>
    /// <returns>The parsed name table.</returns>
    public static NameTable FromFile(string filePath, string hashAlgorithm, Func<ReadOnlySpan<char>, NameKey> hasher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);
        ArgumentNullException.ThrowIfNull(hasher);

        var table = new NameTable(hashAlgorithm);

        foreach (var line in File.ReadLines(filePath))
        {
            var trimmed = line.AsSpan().Trim();
            if (trimmed.IsEmpty || trimmed[0] is '#' || trimmed.StartsWith("//"))
                continue;

            var key = hasher(trimmed);
            table.Add(key, trimmed.ToString());
        }

        return table;
    }
}
