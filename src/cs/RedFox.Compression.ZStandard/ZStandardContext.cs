namespace RedFox.Compression.ZStandard;

internal sealed class ZStandardContext : IDisposable
{
    private nint _context;
    private nint _dictionary;
    private readonly bool _compress;

    internal ZStandardContext(bool compress, int level, byte[] dictionary, long? size)
    {
        _compress = compress;
        if (compress)
        {
            _context = ZStandardInterop.CreateCompressionContext();
        }
        else
        {
            _context = ZStandardInterop.CreateDecompressionContext();
        }
        if (_context == 0)
            throw new CompressionException("Failed to allocate a Zstandard context.");

        try
        {
            if (dictionary.Length != 0)
            {
                if (compress)
                {
                    _dictionary = ZStandardInterop.CreateCompressionDictionary(dictionary, (nuint)dictionary.Length, level);
                }
                else
                {
                    _dictionary = ZStandardInterop.CreateDecompressionDictionary(dictionary, (nuint)dictionary.Length);
                }
                if (_dictionary == 0)
                    throw new CompressionException("Failed to prepare the Zstandard dictionary.");
            }
            if (compress)
            {
                ZStandardInterop.Check(ZStandardInterop.SetParameter(_context, 100, level));
                ZStandardInterop.Check(ZStandardInterop.ReferenceCompressionDictionary(_context, _dictionary));
                ulong pledgedSize = ulong.MaxValue;
                if (size.HasValue)
                    pledgedSize = (ulong)size.Value;

                ZStandardInterop.Check(ZStandardInterop.SetPledgedSize(_context, pledgedSize));
            }
            else
            {
                ZStandardInterop.Check(ZStandardInterop.ReferenceDecompressionDictionary(_context, _dictionary));
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal int Transform(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_context == 0, this);
        nuint result;
        if (_compress)
        {
            ZStandardInterop.Check(ZStandardInterop.ResetCompression(_context, 1));
            result = ZStandardInterop.Compress(_context, destination, (nuint)destination.Length, source, (nuint)source.Length);
        }
        else
        {
            ZStandardInterop.Check(ZStandardInterop.ResetDecompression(_context, 1));
            result = ZStandardInterop.Decompress(_context, destination, (nuint)destination.Length, source, (nuint)source.Length, _dictionary);
        }
        ZStandardInterop.Check(result);
        GC.KeepAlive(this);
        return checked((int)result);
    }

    internal unsafe bool Process(ReadOnlySpan<byte> source, Span<byte> destination, int operation, out int consumed, out int written)
    {
        fixed (byte* inputPointer = source)
        fixed (byte* outputPointer = destination)
        {
            ZStandardBuffer input = new() { Data = inputPointer, Size = (nuint)source.Length };
            ZStandardBuffer output = new() { Data = outputPointer, Size = (nuint)destination.Length };
            nuint result;
            if (_compress)
            {
                result = ZStandardInterop.Check(ZStandardInterop.CompressStream(_context, ref output, ref input, operation));
            }
            else
            {
                result = ZStandardInterop.Check(ZStandardInterop.DecompressStream(_context, ref output, ref input));
            }
            GC.KeepAlive(this);
            consumed = (int)input.Position;
            written = (int)output.Position;
            return result == 0;
        }
    }

    public void Dispose()
    {
        if (_context == 0)
            return;

        if (_compress)
        {
            ZStandardInterop.FreeCompressionContext(_context);
            ZStandardInterop.FreeCompressionDictionary(_dictionary);
        }
        else
        {
            ZStandardInterop.FreeDecompressionContext(_context);
            ZStandardInterop.FreeDecompressionDictionary(_dictionary);
        }

        _context = 0;
        _dictionary = 0;
        GC.SuppressFinalize(this);
    }

    ~ZStandardContext() => Dispose();
}
