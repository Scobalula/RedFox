namespace RedFox.Compression.ZStandard;

internal sealed class SeekableZStandardDecompressionStream : BlockDecompressionStream
{
    private readonly ZStandardCodec _codec;

    internal SeekableZStandardDecompressionStream(Stream source, bool leaveOpen, long? size, int level, byte[] dictionary) : this(source, leaveOpen, ReadIndex(source, size), new ZStandardCodec(level, dictionary)) { }

    private SeekableZStandardDecompressionStream(Stream source, bool leaveOpen, (CompressedBlock[] Blocks, uint[]? Checksums) index, ZStandardCodec codec) : base(source, index.Blocks, (i, input, output) => SeekableZStandardCodec.DecodeFrame(codec, index.Checksums, i, input, output), leaveOpen)
    {
        _codec = codec;
        long start = source.Position;
        try
        {
            for (int i = 0; i < index.Blocks.Length; i++)
            {
                CompressedBlock block = index.Blocks[i];
                if (block.DecompressedLength != 0)
                    continue;

                byte[] input = new byte[block.CompressedLength];
                source.Position = start + block.CompressedOffset;
                source.ReadExactly(input);
                    SeekableZStandardCodec.DecodeFrame(codec, index.Checksums, i, input, []);
            }
        }
        finally
        {
            source.Position = start;
        }
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        _codec.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _codec.Dispose();

        base.Dispose(disposing);
    }

    private static (CompressedBlock[] Blocks, uint[]? Checksums) ReadIndex(Stream source, long? size)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead || !source.CanSeek)
            throw new ArgumentException("Seekable Zstandard requires readable, seekable input.", nameof(source));
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        long start = source.Position;
        long length = size ?? source.Length - start;
        if (length < 17 || length > source.Length - start)
            throw new CompressionException("The seekable Zstandard input length is invalid.");

        try
        {
            source.Position = start + length - 9;
            Span<byte> footerBytes = stackalloc byte[9];
            source.ReadExactly(footerBytes);
            var footer = SeekableZStandardCodec.ReadFooter(footerBytes);
            if (footer.TableSize > length || footer.TableSize > Array.MaxLength)
                throw new CompressionException("The Zstandard seek table length is invalid.");

            byte[] table = new byte[(int)footer.TableSize];
            source.Position = start + length - table.Length;
            source.ReadExactly(table);
            return SeekableZStandardCodec.ReadEntries(table, length - table.Length);
        }
        finally
        {
            source.Position = start;
        }
    }
}
