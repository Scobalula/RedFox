// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Provides methods for parsing name tables from a CSV file format.
/// </summary>
public static class NameTableCsvReader
{
    /// <summary>
    /// Parses a name table from a CSV file, where each line contains a hex string and a corrosponding string.
    /// </summary>
    /// <param name="filePath">The path to the CSV file.</param>
    /// <param name="hashAlgorithm">The hash algorithm that was used to generate the file.</param>
    /// <returns>The parsed name table.</returns>
    public static NameTable FromFile(string filePath, string hashAlgorithm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var table = new NameTable(hashAlgorithm);

        foreach (var line in File.ReadLines(filePath))
        {
            var trimmed = line.AsSpan().Trim();
            if (trimmed.IsEmpty || trimmed[0] is '#' || trimmed.StartsWith("//"))
                continue;

            var commaIndex = trimmed.IndexOf(',');
            if (commaIndex < 1)
                continue;

            var hashSpan = trimmed[..commaIndex].Trim();
            var nameSpan = trimmed[(commaIndex + 1)..].Trim();

            if (hashSpan.IsEmpty || nameSpan.IsEmpty)
                continue;

            var key = NameKey.FromHexString(hashSpan);
            table.Add(key, nameSpan.ToString());
        }

        return table;
    }
}
