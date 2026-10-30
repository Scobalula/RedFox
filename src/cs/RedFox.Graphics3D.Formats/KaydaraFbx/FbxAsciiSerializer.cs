using System.Buffers;
using System.Globalization;
using System.Text;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Reads and writes the human-readable FBX ASCII encoding.
/// </summary>
public static class FbxAsciiSerializer
{
    /// <summary>
    /// Gets the maximum node nesting depth accepted when reading.
    /// </summary>
    public const int MaxDepth = 256;

    /// <summary>
    /// Reads an ASCII FBX document.
    /// </summary>
    /// <param name="data">The complete file contents.</param>
    /// <returns>The parsed document.</returns>
    public static FbxDocument Read(byte[] data)
    {
        ReadOnlySpan<byte>	text		= data;
        int					position	= text.StartsWith("﻿"u8) ? 3 : 0;

        FbxDocument document = new()
        {
            Format	= FbxFormat.Ascii,
            Version	= ReadHeaderVersion(text[position..]),
        };

        Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> names = new Dictionary<string, string>(StringComparer.Ordinal).GetAlternateLookup<ReadOnlySpan<char>>();

        while (true)
        {
            SkipWhitespace(text, ref position);

            if (position >= text.Length)
                break;

            document.Nodes.Add(ReadNode(text, ref position, names, 0));
        }

        if (document.FirstNode("FBXHeaderExtension")?.TryGetChildProperty("FBXVersion", out FbxProperty version) == true && version.IsNumber)
            document.Version = version.AsInt32();

        return document;
    }

    /// <summary>
    /// Writes an ASCII FBX document.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="document">The document to write.</param>
    public static void Write(Stream stream, FbxDocument document)
    {
        using StreamWriter writer = new(stream, new UTF8Encoding(false), 1 << 16, leaveOpen: true);

        int version = document.Version;

        writer.NewLine = "\n";
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"; FBX {version / 1000}.{version / 100 % 10}.{version / 10 % 10} project file"));
        writer.WriteLine("; ----------------------------------------------------");
        writer.WriteLine();

        foreach (FbxNode node in document.Nodes)
        {
            WriteNode(writer, node, 0);
            writer.WriteLine();
        }
    }

    private static int ReadHeaderVersion(ReadOnlySpan<byte> text)
    {
        ReadOnlySpan<byte>	prefix	= "; FBX "u8;
        int					index	= text[..Math.Min(text.Length, 256)].IndexOf(prefix);

        if (index < 0)
            return 7400;

        ReadOnlySpan<byte>	digits	= text[(index + prefix.Length)..];
        int					version	= 0;
        int					count	= 0;

        for (int i = 0; i < digits.Length && count < 3; i++)
        {
            if (char.IsAsciiDigit((char)digits[i]))
            {
                version = version * 10 + digits[i] - '0';
                count++;
            }
            else if (digits[i] != '.')
            {
                break;
            }
        }

        return count == 3 ? version * 10 : 7400;
    }

    private static FbxNode ReadNode(ReadOnlySpan<byte> text, ref int position, Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> names, int depth)
    {
        if (depth >= MaxDepth)
            throw new InvalidDataException("FBX node nesting is too deep.");

        int start = position;

        while (position < text.Length && IsNameChar(text[position]))
            position++;

        if (position == start)
            throw new InvalidDataException($"Unexpected character '{(char)text[position]}' in FBX ASCII data at offset {position}.");

        FbxNode node = new(Intern(names, text[start..position]));

        SkipSpaces(text, ref position);

        if (position >= text.Length || text[position] != ':')
            throw new InvalidDataException($"Expected ':' after FBX node '{node.Name}' at offset {position}.");

        position++;
        ReadValues(text, ref position, node, names);

        if (position < text.Length && text[position] == '{')
        {
            position++;

            while (true)
            {
                SkipWhitespace(text, ref position);

                if (position >= text.Length)
                    throw new InvalidDataException($"FBX node '{node.Name}' is not terminated.");

                if (text[position] == '}')
                {
                    position++;
                    break;
                }

                node.Children.Add(ReadNode(text, ref position, names, depth + 1));
            }
        }

        return node;
    }

    private static void ReadValues(ReadOnlySpan<byte> text, ref int position, FbxNode node, Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> names)
    {
        while (true)
        {
            SkipSpaces(text, ref position);

            if (position >= text.Length)
                return;

            byte current = text[position];

            if (current is (byte)'{' or (byte)'}')
                return;

            if (current is (byte)'\r' or (byte)'\n')
            {
                int lookahead = position;
                SkipWhitespace(text, ref lookahead);

                if (lookahead >= text.Length || text[lookahead] == '}' || IsNodeStart(text, lookahead))
                    return;

                position = lookahead;
                continue;
            }

            if (current == ',')
            {
                position++;
                SkipWhitespace(text, ref position);
                continue;
            }

            if (IsNodeStart(text, position))
                return;

            node.Properties.Add(ReadValue(text, ref position, names));
        }
    }

    private static bool IsNodeStart(ReadOnlySpan<byte> text, int position)
    {
        int start = position;

        while (position < text.Length && IsNameChar(text[position]))
            position++;

        if (position == start)
            return false;

        while (position < text.Length && text[position] is (byte)' ' or (byte)'\t')
            position++;

        return position < text.Length && text[position] == ':';
    }

    private static FbxProperty ReadValue(ReadOnlySpan<byte> text, ref int position, Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> names)
    {
        if (text[position] == '"')
        {
            int length = text[(position + 1)..].IndexOf((byte)'"');

            if (length < 0)
                throw new InvalidDataException($"Unterminated FBX string at offset {position}.");

            ReadOnlySpan<byte> value = text.Slice(position + 1, length);
            position += length + 2;

            string decoded = length <= 64 ? Intern(names, value) : Encoding.UTF8.GetString(value);
            return new FbxProperty(decoded.Contains("&quot;", StringComparison.Ordinal) ? decoded.Replace("&quot;", "\"", StringComparison.Ordinal) : decoded);
        }

        if (text[position] == '*')
            return ReadArray(text, ref position);

        ReadOnlySpan<byte> token = ReadToken(text, ref position);

        if (token.IsEmpty)
            throw new InvalidDataException($"Unexpected character '{(char)text[position]}' in FBX ASCII data at offset {position}.");

        if (long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer))
            return integer is >= int.MinValue and <= int.MaxValue ? new FbxProperty((int)integer) : new FbxProperty(integer);

        if (TryParseReal(token, out double real))
            return new FbxProperty(real);

        if (token.SequenceEqual("T"u8) || token.SequenceEqual("Y"u8))
            return new FbxProperty(true);

        if (token.SequenceEqual("F"u8) || token.SequenceEqual("N"u8))
            return new FbxProperty(false);

        return new FbxProperty(Intern(names, token));
    }

    private static FbxProperty ReadArray(ReadOnlySpan<byte> text, ref int position)
    {
        position++;

        if (!long.TryParse(ReadToken(text, ref position), NumberStyles.None, CultureInfo.InvariantCulture, out long count))
            throw new InvalidDataException($"Invalid FBX array length at offset {position}.");

        SkipWhitespace(text, ref position);

        if (position >= text.Length || text[position] != '{')
            throw new InvalidDataException($"Expected '{{' after FBX array length at offset {position}.");

        position++;
        SkipWhitespace(text, ref position);

        int				capacity	= (int)Math.Min(count, (text.Length - position) / 2 + 1);
        List<long>		integers	= new(capacity);
        List<double>?	reals		= null;

        if (position < text.Length && text[position] is (byte)'a' or (byte)'A')
        {
            position++;
            SkipSpaces(text, ref position);

            if (position >= text.Length || text[position] != ':')
                throw new InvalidDataException($"Expected ':' in FBX array at offset {position}.");

            position++;
        }

        while (true)
        {
            SkipWhitespace(text, ref position);

            if (position >= text.Length)
                throw new InvalidDataException("FBX array is not terminated.");

            if (text[position] == '}')
            {
                position++;
                break;
            }

            if (text[position] == ',')
            {
                position++;
                continue;
            }

            ReadOnlySpan<byte> token = ReadToken(text, ref position);

            if (reals is null && long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer))
            {
                integers.Add(integer);
                continue;
            }

            if (!TryParseReal(token, out double real))
                throw new InvalidDataException($"Invalid number in FBX array at offset {position}.");

            if (reals is null)
            {
                reals = new List<double>(capacity);

                foreach (long value in integers)
                    reals.Add(value);
            }

            reals.Add(real);
        }

        if (reals is not null)
            return new FbxProperty(reals.ToArray());

        foreach (long value in integers)
        {
            if (value is < int.MinValue or > int.MaxValue)
                return new FbxProperty(integers.ToArray());
        }

        return new FbxProperty(integers.ConvertAll(static value => (int)value).ToArray());
    }

    private static ReadOnlySpan<byte> ReadToken(ReadOnlySpan<byte> text, ref int position)
    {
        int start = position;

        while (position < text.Length && !IsDelimiter(text[position]))
            position++;

        return text[start..position];
    }

    private static bool TryParseReal(ReadOnlySpan<byte> token, out double value)
    {
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        Span<byte>	lower		= stackalloc byte[Math.Min(token.Length, 64)];
        int			length		= Ascii.ToLower(token[..lower.Length], lower, out int written) == OperationStatus.Done ? written : 0;
        bool		negative	= length > 0 && lower[0] == '-';

        lower = lower[..length].TrimStart("+-"u8);

        if (lower.StartsWith("inf"u8) || lower.IndexOf("#inf"u8) >= 0)
        {
            value = negative ? double.NegativeInfinity : double.PositiveInfinity;
            return true;
        }

        if (lower.StartsWith("nan"u8) || lower.StartsWith("snan"u8) || lower.StartsWith("qnan"u8) || lower.StartsWith("ind"u8) || lower.IndexOf("#nan"u8) >= 0 || lower.IndexOf("#ind"u8) >= 0 || lower.IndexOf("#qnan"u8) >= 0 || lower.IndexOf("#snan"u8) >= 0)
        {
            value = double.NaN;
            return true;
        }

        value = 0;
        return false;
    }

    private static string Intern(Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> names, ReadOnlySpan<byte> value)
    {
        if (value.Length > 256)
            return Encoding.UTF8.GetString(value);

        Span<char>	chars	= stackalloc char[value.Length];
        int			count	= Encoding.UTF8.GetChars(value, chars);

        if (names.TryGetValue(chars[..count], out string? interned))
            return interned;

        string created = chars[..count].ToString();
        names[created] = created;
        return created;
    }

    private static void SkipSpaces(ReadOnlySpan<byte> text, ref int position)
    {
        while (position < text.Length)
        {
            byte current = text[position];

            if (current == ';')
            {
                while (position < text.Length && text[position] != '\n')
                    position++;

                return;
            }

            if (current is not ((byte)' ' or (byte)'\t'))
                return;

            position++;
        }
    }

    private static void SkipWhitespace(ReadOnlySpan<byte> text, ref int position)
    {
        while (position < text.Length)
        {
            byte current = text[position];

            if (current == ';')
            {
                while (position < text.Length && text[position] != '\n')
                    position++;

                continue;
            }

            if (current is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
                return;

            position++;
        }
    }

    private static bool IsNameChar(byte value)
    {
        return char.IsAsciiLetterOrDigit((char)value) || value is (byte)'_' or (byte)'|' or (byte)'-' or (byte)'.';
    }

    private static bool IsDelimiter(byte value)
    {
        return value is (byte)',' or (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'{' or (byte)'}' or (byte)';' or (byte)'"' or (byte)':';
    }

    private static void WriteNode(StreamWriter writer, FbxNode node, int depth)
    {
        WriteIndent(writer, depth);
        writer.Write(node.Name);
        writer.Write(':');

        for (int i = 0; i < node.Properties.Count; i++)
        {
            writer.Write(i == 0 ? " " : ", ");
            WriteProperty(writer, node.Properties[i], depth);
        }

        if (node.Children.Count == 0 && node.Properties.Count > 0)
        {
            writer.WriteLine();
            return;
        }

        writer.WriteLine(" {");

        foreach (FbxNode child in node.Children)
            WriteNode(writer, child, depth + 1);

        WriteIndent(writer, depth);
        writer.WriteLine('}');
    }

    private static void WriteProperty(StreamWriter writer, FbxProperty property, int depth)
    {
        switch (property.Type)
        {
            case FbxPropertyType.Boolean:
                writer.Write(GetBooleanToken(property));
                break;
            case FbxPropertyType.Int16:
            case FbxPropertyType.Int32:
            case FbxPropertyType.Int64:
                WriteInt64(writer, property.AsInt64());
                break;
            case FbxPropertyType.Single:
                WriteSingle(writer, property.AsSingle());
                break;
            case FbxPropertyType.Double:
                WriteDouble(writer, property.AsDouble());
                break;
            case FbxPropertyType.String:
                WriteString(writer, property.AsString());
                break;
            case FbxPropertyType.Raw:
                writer.Write('"');
                writer.Write(Convert.ToBase64String(property.AsBytes()));
                writer.Write('"');
                break;
            default:
                WriteArray(writer, property, depth);
                break;
        }
    }

    private static char GetBooleanToken(FbxProperty property)
    {
        char raw = (char)property.AsInt64();

        if (char.IsAsciiLetter(raw))
            return raw;

        return property.AsBoolean() ? 'T' : 'F';
    }

    private static void WriteArray(StreamWriter writer, FbxProperty property, int depth)
    {
        writer.Write('*');
        WriteInt64(writer, property.ArrayLength);
        writer.WriteLine(" {");
        WriteIndent(writer, depth + 1);
        writer.Write("a: ");

        switch (property.Value)
        {
            case float[] values:
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                        writer.Write(',');

                    WriteSingle(writer, values[i]);
                }
                break;
            case double[] values:
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                        writer.Write(',');

                    WriteDouble(writer, values[i]);
                }
                break;
            default:
                long[] integers = property.ToInt64Array();

                for (int i = 0; i < integers.Length; i++)
                {
                    if (i > 0)
                        writer.Write(',');

                    WriteInt64(writer, integers[i]);
                }
                break;
        }

        writer.WriteLine();
        WriteIndent(writer, depth);
        writer.Write('}');
    }

    private static void WriteString(StreamWriter writer, string value)
    {
        int separator = value.IndexOf("\0\u0001", StringComparison.Ordinal);

        if (separator >= 0)
            value = $"{value[(separator + 2)..]}::{value[..separator]}";

        writer.Write('"');
        writer.Write(value.Contains('"') ? value.Replace("\"", "&quot;", StringComparison.Ordinal) : value);
        writer.Write('"');
    }

    private static void WriteInt64(StreamWriter writer, long value)
    {
        Span<char> buffer = stackalloc char[24];
        value.TryFormat(buffer, out int written, default, CultureInfo.InvariantCulture);
        writer.Write(buffer[..written]);
    }

    private static void WriteSingle(StreamWriter writer, float value)
    {
        Span<char> buffer = stackalloc char[32];
        value.TryFormat(buffer, out int written, "R", CultureInfo.InvariantCulture);
        writer.Write(buffer[..written]);
    }

    private static void WriteDouble(StreamWriter writer, double value)
    {
        Span<char> buffer = stackalloc char[32];
        value.TryFormat(buffer, out int written, "R", CultureInfo.InvariantCulture);
        writer.Write(buffer[..written]);
    }

    private static void WriteIndent(StreamWriter writer, int depth)
    {
        for (int i = 0; i < depth; i++)
            writer.Write('\t');
    }
}
