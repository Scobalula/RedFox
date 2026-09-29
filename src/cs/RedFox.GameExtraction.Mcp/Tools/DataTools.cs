using System.Buffers.Binary;
using System.ComponentModel;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace RedFox.GameExtraction.Mcp.Tools;

[McpServerToolType]
internal sealed class DataTools(AssetDataReader dataReader)
{
    private const int MaximumDumpLength = 4096;

    [McpServerTool(Name = "hex_dump"), Description("Returns a hex and ASCII dump of an asset's raw bytes, capped at 4096 bytes per call.")]
    public async Task<string> HexDump([Description("Full asset name.")] string name, [Description("Start offset in bytes.")] long offset = 0, [Description("Number of bytes, max 4096.")] int length = 256)
    {
        byte[] data = await dataReader.ReadAsync(dataReader.GetAsset(name), offset, Math.Min(length, MaximumDumpLength));
        StringBuilder builder = new();

        for (int row = 0; row < data.Length; row += 16)
        {
            ReadOnlySpan<byte> line = data.AsSpan(row, Math.Min(16, data.Length - row));

            builder.Append($"{offset + row:X8}  ");

            for (int index = 0; index < 16; index++)
            {
                builder.Append(index < line.Length ? $"{line[index]:X2} " : "   ");
            }

            builder.Append(' ');

            foreach (byte value in line)
            {
                builder.Append(value is >= 0x20 and < 0x7F ? (char)value : '.');
            }

            builder.AppendLine();
        }

        return data.Length == 0 ? "No data at that offset." : builder.ToString();
    }

    [McpServerTool(Name = "read_values"), Description("Reads typed values from an asset's raw bytes. Types: u8 i8 u16 i16 u32 i32 u64 i64 f16 f32 f64 cstring.")]
    public async Task<string> ReadValues([Description("Full asset name.")] string name, [Description("Start offset in bytes.")] long offset, [Description("Value type.")] string type, [Description("Number of values, max 256.")] int count = 1, [Description("Read big endian.")] bool bigEndian = false)
    {
        Asset asset = dataReader.GetAsset(name);
        string kind = type.ToLowerInvariant();
        int size = kind switch
        {
            "u8" or "i8" => 1,
            "u16" or "i16" or "f16" => 2,
            "u32" or "i32" or "f32" => 4,
            "u64" or "i64" or "f64" => 8,
            "cstring" => 256,
            _ => throw new McpException($"Unknown type '{type}'.")
        };

        count = Math.Clamp(count, 1, 256);

        if (kind == "cstring")
        {
            byte[] text = await dataReader.ReadAsync(asset, offset, size);
            int end = Array.IndexOf(text, (byte)0);

            return Encoding.UTF8.GetString(text, 0, end < 0 ? text.Length : end);
        }

        byte[] data = await dataReader.ReadAsync(asset, offset, size * count);
        List<string> values = [];

        for (int index = 0; index + size <= data.Length; index += size)
        {
            values.Add(Format(data.AsSpan(index, size), kind, bigEndian));
        }

        return values.Count == 0 ? "No data at that offset." : string.Join(' ', values);
    }

    [McpServerTool(Name = "find_bytes"), Description("Finds all offsets of a hex byte pattern in an asset, e.g. '4D 44 4C 00'.")]
    public async Task<string> FindBytes([Description("Full asset name.")] string name, [Description("Hex bytes, spaces optional.")] string hexPattern, [Description("Maximum offsets to return.")] int limit = 50)
    {
        byte[] pattern = Convert.FromHexString(hexPattern.Replace(" ", string.Empty));

        return FormatOffsets(await dataReader.ReadAllAsync(dataReader.GetAsset(name)), pattern, limit);
    }

    [McpServerTool(Name = "find_string"), Description("Finds all offsets of a text string in an asset, searching both ASCII and UTF-16.")]
    public async Task<string> FindString([Description("Full asset name.")] string name, [Description("Text to search for.")] string text, [Description("Maximum offsets per encoding.")] int limit = 50)
    {
        byte[] data = await dataReader.ReadAllAsync(dataReader.GetAsset(name));

        return $"ASCII: {FormatOffsets(data, Encoding.ASCII.GetBytes(text), limit)}\nUTF-16: {FormatOffsets(data, Encoding.Unicode.GetBytes(text), limit)}";
    }

    [McpServerTool(Name = "save_raw"), Description("Writes an asset's raw bytes to a file on disk, e.g. for use in an external hex editor or disassembler.")]
    public async Task<string> SaveRaw([Description("Full asset name.")] string name, [Description("Destination file path.")] string path)
    {
        byte[] data = await dataReader.ReadAllAsync(dataReader.GetAsset(name));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllBytesAsync(path, data);

        return $"Wrote {data.Length} bytes to {path}.";
    }

    private static string FormatOffsets(byte[] data, byte[] pattern, int limit)
    {
        List<string> offsets = [];
        int position = 0;

        while (pattern.Length > 0 && offsets.Count < limit && position <= data.Length - pattern.Length)
        {
            int found = data.AsSpan(position).IndexOf(pattern);

            if (found < 0)
            {
                break;
            }

            offsets.Add($"0x{position + found:X}");
            position += found + 1;
        }

        return offsets.Count == 0 ? "no matches" : string.Join(' ', offsets);
    }

    private static string Format(ReadOnlySpan<byte> bytes, string type, bool bigEndian)
    {
        return type switch
        {
            "u8" => bytes[0].ToString(),
            "i8" => ((sbyte)bytes[0]).ToString(),
            "u16" => (bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : BinaryPrimitives.ReadUInt16LittleEndian(bytes)).ToString(),
            "i16" => (bigEndian ? BinaryPrimitives.ReadInt16BigEndian(bytes) : BinaryPrimitives.ReadInt16LittleEndian(bytes)).ToString(),
            "f16" => (bigEndian ? BinaryPrimitives.ReadHalfBigEndian(bytes) : BinaryPrimitives.ReadHalfLittleEndian(bytes)).ToString(),
            "u32" => (bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(bytes) : BinaryPrimitives.ReadUInt32LittleEndian(bytes)).ToString(),
            "i32" => (bigEndian ? BinaryPrimitives.ReadInt32BigEndian(bytes) : BinaryPrimitives.ReadInt32LittleEndian(bytes)).ToString(),
            "f32" => (bigEndian ? BinaryPrimitives.ReadSingleBigEndian(bytes) : BinaryPrimitives.ReadSingleLittleEndian(bytes)).ToString(),
            "u64" => (bigEndian ? BinaryPrimitives.ReadUInt64BigEndian(bytes) : BinaryPrimitives.ReadUInt64LittleEndian(bytes)).ToString(),
            "i64" => (bigEndian ? BinaryPrimitives.ReadInt64BigEndian(bytes) : BinaryPrimitives.ReadInt64LittleEndian(bytes)).ToString(),
            _ => (bigEndian ? BinaryPrimitives.ReadDoubleBigEndian(bytes) : BinaryPrimitives.ReadDoubleLittleEndian(bytes)).ToString()
        };
    }
}
