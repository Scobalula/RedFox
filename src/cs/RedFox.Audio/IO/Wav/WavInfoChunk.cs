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

namespace RedFox.Audio.IO.Wav;

internal static class WavInfoChunk
{
    private const int IdLength = 4;

    private static readonly (string Id, string Field)[] Fields =
    [
        ("INAM", "TITLE"),
        ("IART", "ARTIST"),
        ("IPRD", "ALBUM"),
        ("ICRD", "DATE"),
        ("IGNR", "GENRE"),
        ("ICMT", "COMMENT"),
        ("ICOP", "COPYRIGHT"),
        ("ISFT", "ENCODER"),
        ("ITRK", "TRACKNUMBER"),
    ];

    private static ReadOnlySpan<byte> ListType => "INFO"u8;

    public static bool IsInfo(ReadOnlySpan<byte> chunk) => chunk.StartsWith(ListType);

    public static void Read(ReadOnlySpan<byte> chunk, IDictionary<string, string> tags)
    {
        int offset = ListType.Length;

        while (offset + 8 <= chunk.Length)
        {
            string id = Encoding.ASCII.GetString(chunk.Slice(offset, IdLength));
            int size = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(chunk[(offset + 4)..]), (uint)(chunk.Length - offset - 8));

            tags[GetField(id)] = Encoding.UTF8.GetString(chunk.Slice(offset + 8, size)).TrimEnd('\0');
            offset += 8 + size + (size & 1);
        }
    }

    public static byte[] Create(IReadOnlyDictionary<string, string> tags)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        foreach ((string field, string value) in tags)
        {
            if (GetId(field) is not { } id)
                continue;

            byte[] bytes = Encoding.UTF8.GetBytes(value);

            writer.Write(Encoding.ASCII.GetBytes(id));
            writer.Write(bytes.Length + 1);
            writer.Write(bytes);
            writer.Write((byte)0);

            if ((bytes.Length & 1) == 0)
                writer.Write((byte)0);
        }

        if (stream.Length == 0)
            return [];

        writer.Flush();
        return [.. ListType, .. stream.ToArray()];
    }

    private static string GetField(string id) => Array.Find(Fields, entry => entry.Id == id).Field ?? id;

    private static string? GetId(string field)
    {
        string? id = Array.Find(Fields, entry => entry.Field.Equals(field, StringComparison.OrdinalIgnoreCase)).Id;

        if (id is null && field.Length == IdLength && field[0] == 'I' && field.All(char.IsAsciiLetterUpper))
            id = field;

        return id;
    }
}
