using CallOfFile;

namespace RedFox.Graphics3D.Formats.IwEngine;

internal sealed class XAssetTokenStream : IDisposable
{
    private readonly TokenReader reader;

    private TokenData? buffered;

    public XAssetTokenStream(Stream stream)
    {
        if (!stream.CanSeek)
            throw new ArgumentException("XAsset input streams must support seeking.", nameof(stream));

        long position = stream.Position;
        Span<byte> magic = stackalloc byte[5];
        bool binary = stream.ReadAtLeast(magic, magic.Length, throwOnEndOfStream: false) == magic.Length && magic.SequenceEqual("*LZ4*"u8);
        stream.Position = position;

        reader = binary ? new BinaryTokenReader(stream) : new ExportTokenReader(new StreamReader(stream, leaveOpen: true));
    }

    public TokenData? Peek()
    {
        buffered ??= ReadMeaningful();
        return buffered;
    }

    public TokenData Read()
    {
        TokenData result = Peek() ?? throw new EndOfStreamException("Unexpected end of XAsset token stream.");
        buffered = null;
        return result;
    }

    public TokenData Expect(params ReadOnlySpan<string> names)
    {
        TokenData result = Read();

        if (!names.Contains(result.Token.Name))
            throw new InvalidDataException($"Expected XAsset token '{string.Join("' or '", names)}', found '{result.Token.Name}'.");

        return result;
    }

    public TokenData MoveTo(params ReadOnlySpan<string> names)
    {
        while (Peek() is { } token)
        {
            Read();

            if (names.Contains(token.Token.Name))
                return token;
        }

        throw new InvalidDataException($"The XAsset does not contain a '{string.Join("' or '", names)}' token.");
    }

    public void Dispose() => reader.Dispose();

    private TokenData? ReadMeaningful()
    {
        TokenData? token;

        do
        {
            token = reader.ReadToken();
        }
        while (token?.Token.DataType == TokenDataType.Comment);

        return token;
    }
}
