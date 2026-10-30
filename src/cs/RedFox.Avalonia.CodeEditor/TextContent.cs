using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Unicode;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Tells text from binary data by its content, the way VS Code does, so the file's name plays no part.
/// </summary>
public static class TextContent
{
    private const int SampleLength = 512;

    /// <summary>
    /// Decodes data when it is text.
    /// </summary>
    /// <param name="data">The file contents.</param>
    /// <param name="text">The decoded text, without a byte order mark or trailing NUL bytes.</param>
    /// <returns><see langword="false"/> when the data is binary.</returns>
    public static bool TryDecode(ReadOnlySpan<byte> data, [NotNullWhen(true)] out string? text)
    {
        text = null;

        if (data.StartsWith<byte>([0xEF, 0xBB, 0xBF]))
        {
            text = Encoding.UTF8.GetString(data[3..]);
        }
        else if (data.StartsWith<byte>([0xFF, 0xFE]))
        {
            text = Encoding.Unicode.GetString(data[2..]);
        }
        else if (data.StartsWith<byte>([0xFE, 0xFF]))
        {
            text = Encoding.BigEndianUnicode.GetString(data[2..]);
        }
        else
        {
            Encoding? encoding = DetectEncoding(data);
            if (encoding is null)
            {
                return false;
            }

            text = encoding.GetString(data);
        }

        text = text.TrimEnd('\0');
        return true;
    }

    private static Encoding? DetectEncoding(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<byte> sample = data[..Math.Min(data.Length, SampleLength)];

        if (sample.IndexOf((byte)0) < 0)
        {
            return Utf8.IsValid(data) ? Encoding.UTF8 : Encoding.Latin1;
        }

        bool isLittleEndian	= true;
        bool isBigEndian	= true;

        for (int index = 0; index < sample.Length; index++)
        {
            bool isOdd	= index % 2 == 1;
            bool isZero	= sample[index] == 0;

            isLittleEndian &= isOdd == isZero;
            isBigEndian &= isOdd != isZero;
        }

        if (isLittleEndian)
        {
            return Encoding.Unicode;
        }

        return isBigEndian ? Encoding.BigEndianUnicode : null;
    }
}
