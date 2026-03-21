using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox;

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
        Span<char> buffer = stackalloc char[2];

        var pattern = new List<byte>(hexString.Length);
        var mask = new List<byte>(hexString.Length);

        for (int i = 0, j = 0; i < 2 && j < hexString.Length; j++)
        {
            if (char.IsWhiteSpace(hexString[j]))
                continue;
            buffer[i++] = hexString[j];

            if (i == 2)
            {
                i = 0;

                // Check if unknown vs hex
                if (buffer[0] == '?' || buffer[1] == '?')
                {
                    pattern.Add(0);
                    mask.Add(0xFF);
                }
                else if (byte.TryParse(buffer, NumberStyles.HexNumber, null, out byte b))
                {
                    pattern.Add(b);
                    mask.Add(0);
                }
            }
        }

        return new([.. pattern], [.. mask]);
    }
}
