using System;
using System.Collections.Generic;
using System.Globalization;

namespace RedFox.Patterns;

/// <summary>
/// A class to provide methods for interacting with a byte <see cref="Pattern{T}"/>.
/// </summary>
public static class BytePattern
{
    /// <summary>
    /// Parses a byte <see cref="Pattern{T}"/> from the provided hex string.
    /// </summary>
    /// <param name="hexString">The hex string to parse.</param>
    /// <returns>The resulting pattern.</returns>
    public static Pattern<byte> Parse(string hexString)
    {
        ArgumentNullException.ThrowIfNull(hexString);
        var pattern = new List<byte>();
        var mask = new List<byte>();

        foreach (string token in hexString.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            for (int index = 0; index < token.Length; index += 2)
            {
                int length = Math.Min(2, token.Length - index);
                if (token[index] == '?' || length == 2 && token[index + 1] == '?')
                {
                    pattern.Add(0);
                    mask.Add(0xFF);
                }
                else if (length == 2 && byte.TryParse(token.AsSpan(index, length), NumberStyles.HexNumber, null, out byte value))
                {
                    pattern.Add(value);
                    mask.Add(0);
                }
            }
        }

        return new([.. pattern], [.. mask]);
    }
}
