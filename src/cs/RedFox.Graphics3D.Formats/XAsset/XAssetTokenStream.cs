using CallOfFile;

namespace RedFox.Graphics3D.Formats.XAsset;

internal sealed class XAssetTokenStream : IDisposable
{
    private static readonly byte[] Magic = "*LZ4*"u8.ToArray();
    private readonly TokenReader reader;
    private TokenData? buffered;

    public XAssetTokenStream(Stream stream)
    {
        if (!stream.CanSeek)
            throw new ArgumentException("XAsset input streams must support seeking.", nameof(stream));
        long position = stream.Position;
        Span<byte> magic = stackalloc byte[Magic.Length];
        bool binary = stream.Read(magic) == Magic.Length && magic.SequenceEqual(Magic);
        stream.Position = position;
        reader = binary ? new BinaryTokenReader(stream) : new ExportTokenReader(stream);
    }

    public TokenData? Peek()
    {
        buffered ??= ReadMeaningful();
        return buffered;
    }

    public TokenData Read()
    {
        TokenData? result = Peek();
        if (result is null)
            throw new EndOfStreamException("Unexpected end of XAsset token stream.");
        buffered = null;
        return result;
    }

    public TokenData Expect(string name)
    {
        TokenData result = Read();
        if (result.Token.Name != name)
            throw new InvalidDataException($"Expected XAsset token '{name}', found '{result.Token.Name}'.");
        return result;
    }

    public TokenData ExpectEither(string first, string second)
    {
        TokenData result = Read();
        if (result.Token.Name != first && result.Token.Name != second)
            throw new InvalidDataException($"Expected XAsset token '{first}' or '{second}', found '{result.Token.Name}'.");
        return result;
    }

    public TokenData MoveTo(string name)
    {
        while (Peek() is { } token)
        {
            if (token.Token.Name == name)
                return Read();
            Read();
        }
        throw new InvalidDataException($"The XAsset does not contain a '{name}' token.");
    }

    public TokenData MoveToEither(string first, string second)
    {
        while (Peek() is { } token)
        {
            if (token.Token.Name == first || token.Token.Name == second)
                return Read();
            Read();
        }
        throw new InvalidDataException($"The XAsset does not contain a '{first}' or '{second}' token.");
    }

    public void Dispose() => reader.Dispose();

    private TokenData? ReadMeaningful()
    {
        TokenData? token;
        do
        {
            token = reader.RequestNextToken();
        }
        while (token?.Token.Name is ";" or "//");
        return token;
    }
}
