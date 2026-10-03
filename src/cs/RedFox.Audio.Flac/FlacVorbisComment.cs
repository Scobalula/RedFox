// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;
using System.Text;

namespace RedFox.Audio.Flac;

internal sealed class FlacVorbisComment(string vendor, List<string> comments)
{
    public string Vendor { get; } = vendor;

    public List<string> Comments { get; } = comments;

    public static FlacVorbisComment Parse(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        string vendor = ReadString(data, ref offset) ?? string.Empty;
        List<string> comments = [];
        uint count = offset + 4 <= data.Length ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) : 0;

        offset += 4;

        for (uint i = 0; i < count && ReadString(data, ref offset) is { } comment; i++)
            comments.Add(comment);

        return new FlacVorbisComment(vendor, comments);
    }

    public Dictionary<string, string> GetFields()
    {
        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);

        foreach (string comment in Comments)
        {
            int separator = comment.IndexOf('=');

            if (separator <= 0)
                continue;

            string field = comment[..separator];
            string value = comment[(separator + 1)..];

            fields[field] = fields.TryGetValue(field, out string? existing) ? $"{existing}; {value}" : value;
        }

        return fields;
    }

    public byte[] ToBytes()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        WriteString(writer, Vendor);
        writer.Write(Comments.Count);

        foreach (string comment in Comments)
            WriteString(writer, comment);

        writer.Flush();
        return stream.ToArray();
    }

    private static string? ReadString(ReadOnlySpan<byte> data, ref int offset)
    {
        if (offset + 4 > data.Length)
            return null;

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);

        if (length > (uint)(data.Length - offset - 4))
            return null;

        string value = Encoding.UTF8.GetString(data.Slice(offset + 4, (int)length));
        offset += 4 + (int)length;

        return value;
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);

        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
